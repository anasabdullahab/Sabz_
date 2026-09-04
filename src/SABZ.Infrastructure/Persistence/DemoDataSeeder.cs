using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;

namespace SABZ.Infrastructure.Persistence;

/// <summary>
/// Startup backfill + demo-content seeder, run once after migrations:
///
///  1. Links existing free-text crops to the CropCatalog by name so the
///     Prompt 7 monitoring-rule matching works retroactively (historical bug:
///     the crop form never sent CropCatalogId, so no rules ever matched).
///  2. (Re)generates monitoring checks for catalog-linked crops with planting
///     dates and creates due MonitoringDue notifications - reusing the real
///     application services so behaviour stays identical to the API paths.
///  3. Seeds demo community posts/comments from two clearly-labelled demo
///     accounts so the Kisan Network feed is never empty. Demo rows use fixed
///     GUIDs; the whole seeder is idempotent and safe on every startup.
///
/// Any failure is logged and swallowed - seeding must never block startup.
/// </summary>
public static class DemoDataSeeder
{
    // Fixed demo identities. Nobody logs in as these accounts: they carry
    // random unguessable password hashes and exist only to author seed content.
    private static readonly Guid TeamUserId = new("7d2f5a9c-3b1e-4f8a-9c46-2a8d5e1b0f31");
    private static readonly Guid FarmerDemoUserId = new("5c1e8b3a-6d4f-4e2b-b7a9-8f0c3d5e2a17");

    // Common Urdu crop names -> catalog names (extra safety: crops may be
    // recorded with Urdu names via the picker's Urdu display labels).
    private static readonly Dictionary<string, string> UrduToCatalogName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["گندم"] = "Wheat",
        ["کنک"] = "Wheat",
        ["دھان"] = "Rice",
        ["چاول"] = "Rice",
        ["کپاس"] = "Cotton",
        ["آلو"] = "Potato",
        ["ٹماٹر"] = "Tomato",
        ["پیاز"] = "Onion",
        ["گنا"] = "Sugarcane",
        ["مکئی"] = "Maize"
    };

    public static async Task SeedAsync(SabzDbContext db, IServiceProvider services, ILogger logger)
    {
        try
        {
            var linked = await BackfillCropCatalogLinksAsync(db);
            var checksCreated = await RegenerateMonitoringAsync(db, services);
            var (users, posts, comments) = await SeedCommunityContentAsync(db, services);

            logger.LogInformation(
                "Demo/backfill seed: {LinkedCrops} crops catalog-linked, {Checks} monitoring checks generated, {Users} demo users, {Posts} community posts, {Comments} comments added.",
                linked, checksCreated, users, posts, comments);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Demo/backfill seed failed; application continues normally.");
        }
    }

    // ------------------------------------------------------------------
    //  1. Crop -> CropCatalog backfill by name
    // ------------------------------------------------------------------
    private static async Task<int> BackfillCropCatalogLinksAsync(SabzDbContext db)
    {
        var catalog = await db.CropCatalog
            .AsNoTracking()
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        if (catalog.Count == 0) return 0;

        var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in catalog)
        {
            byName.TryAdd(c.Name.Trim(), c.Id);
            var paren = c.Name.IndexOf('(');
            if (paren > 0)
                byName.TryAdd(c.Name[..paren].Trim(), c.Id);
        }

        var crops = await db.Crops
            .Where(c => c.CropCatalogId == null && c.CropName != null)
            .ToListAsync();

        var linked = 0;
        foreach (var crop in crops)
        {
            var name = crop.CropName.Trim();

            if (byName.TryGetValue(name, out var id))
            {
                crop.CropCatalogId = id;
                linked++;
                continue;
            }

            // Urdu aliases for the common crops.
            if (UrduToCatalogName.TryGetValue(name, out var catalogName) && byName.TryGetValue(catalogName, out id))
            {
                crop.CropCatalogId = id;
                linked++;
                continue;
            }

            // Prefix match: "Chili" -> "Chili Pepper", "Gram" -> "Gram (Chickpea)".
            var prefix = catalog.FirstOrDefault(c => c.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
            if (prefix != null)
            {
                crop.CropCatalogId = prefix.Id;
                linked++;
            }
        }

        if (linked > 0)
            await db.SaveChangesAsync();

        return linked;
    }

    // ------------------------------------------------------------------
    //  2. Monitoring checks + due notifications via the real services
    // ------------------------------------------------------------------
    private static async Task<int> RegenerateMonitoringAsync(SabzDbContext db, IServiceProvider services)
    {
        var monitoring = services.GetRequiredService<IMonitoringService>();

        var crops = await db.Crops
            .AsNoTracking()
            .Where(c => c.PlantingDate != null && c.CropCatalogId != null)
            .Select(c => new { c.Id, UserId = c.Farm.UserId })
            .ToListAsync();

        var checksCreated = 0;
        foreach (var crop in crops)
        {
            var result = await monitoring.EnsureChecksForCropAsync(crop.UserId, crop.Id);
            checksCreated += result.ChecksCreated;
        }

        // GetDueChecksAsync lazily (and idempotently) creates MonitoringDue
        // notifications for every already-due check - the same code path the
        // API uses, so farmers see reminders without opening Monitoring first.
        var userIds = crops.Select(c => c.UserId).Distinct().ToList();
        foreach (var userId in userIds)
        {
            await monitoring.GetDueChecksAsync(userId);
        }

        return checksCreated;
    }

    // ------------------------------------------------------------------
    //  3. Demo community content (fixed GUIDs -> fully idempotent)
    // ------------------------------------------------------------------
    private static async Task<(int users, int posts, int comments)> SeedCommunityContentAsync(SabzDbContext db, IServiceProvider services)
    {
        var created = (users: 0, posts: 0, comments: 0);

        var existingUserIds = await db.Users.Select(u => u.Id).ToListAsync();
        var passwordService = services.GetRequiredService<IPasswordService>();

        if (!existingUserIds.Contains(TeamUserId))
        {
            db.Users.Add(NewDemoUser(TeamUserId, "SABZ Agronomy Team", "agronomy@team.sabz.pk", passwordService));
            created.users++;
        }
        if (!existingUserIds.Contains(FarmerDemoUserId))
        {
            db.Users.Add(NewDemoUser(FarmerDemoUserId, "Ahmed Raza", "ahmed.raza@demo.sabz.pk", passwordService));
            created.users++;
        }
        if (created.users > 0)
            await db.SaveChangesAsync();

        var existingPostIds = await db.CommunityPosts.Select(p => p.Id).ToListAsync();
        var existingCommentIds = await db.CommunityComments.Select(c => c.Id).ToListAsync();
        var now = DateTime.UtcNow;

        // (post, author, age, comments[])
        var seed = new (Guid Id, Guid UserId, string Content, TimeSpan Age, (Guid Id, Guid UserId, string Content, TimeSpan Age)[] Comments)[]
        {
            (
                new Guid("a1b2c3d4-1111-4aaa-8bbb-000000000001"), FarmerDemoUserId,
                "السلام علیکم! گندم کی فصل پر پتوں میں زرد دھاریاں نظر آ رہی ہیں۔ کیا یہ rust ہے؟ ملتان کے قریب میرا فارم ہے، فصل 45 دن کی ہو گئی ہے۔ کون سا سپرے مؤثر ہو گا؟",
                TimeSpan.FromHours(2),
                new[]
                {
                    (
                        new Guid("a1b2c3d4-1111-4aaa-8bbb-000000010001"), TeamUserId,
                        "45 din ki gandam par yellow stripes aksar yellow rust ke nishan hote hain. Please affected patti ki qareeb se tasveer Disease Camera par upload karein — tasdeeq aur sahi khurak ki hidayat milegi. Andha dhundh spray se bachein.",
                        TimeSpan.FromHours(1)
                    )
                }
            ),
            (
                new Guid("a1b2c3d4-1111-4aaa-8bbb-000000000002"), TeamUserId,
                "🌾 Wheat reminder: 30 days after planting is the key leaf-health check. Look for rust-coloured pustules, yellowing and insect damage on the leaves. Early detection saves the season — if you are unsure, use the Disease Camera for a free AI check!",
                TimeSpan.FromHours(6),
                new[]
                {
                    (
                        new Guid("a1b2c3d4-1111-4aaa-8bbb-000000020001"), FarmerDemoUserId,
                        "Shukriya! Bohat useful tip. Kal hi apne khet ka round laga hoon.",
                        TimeSpan.FromHours(5)
                    )
                }
            ),
            (
                new Guid("a1b2c3d4-1111-4aaa-8bbb-000000000003"), FarmerDemoUserId,
                "Anyone in the Sahiwal area selling a used seed drill? A rotavator would also work. Please message me here on Kisan Network. Cash payment, ready this season.",
                TimeSpan.FromHours(26),
                Array.Empty<(Guid, Guid, string, TimeSpan)>()
            ),
            (
                new Guid("a1b2c3d4-1111-4aaa-8bbb-000000000004"), TeamUserId,
                "💡 Fertilizer tip for wheat: split your urea — half at sowing (with DAP) and half at the first irrigation. Late, heavy nitrogen makes rust worse and lodging more likely. Ask the AI Agronomist for a schedule tailored to your own crop.",
                TimeSpan.FromDays(2),
                new[]
                {
                    (
                        new Guid("a1b2c3d4-1111-4aaa-8bbb-000000040001"), FarmerDemoUserId,
                        "Good point — I was applying all the urea at once. Will try the split schedule this season.",
                        TimeSpan.FromDays(1)
                    ),
                    (
                        new Guid("a1b2c3d4-1111-4aaa-8bbb-000000040002"), TeamUserId,
                        "You can also record every fertilizer purchase in the Financial Ledger — the per-crop profit view then shows exactly what each input returned.",
                        TimeSpan.FromDays(1).Add(TimeSpan.FromHours(-2))
                    )
                }
            ),
            (
                new Guid("a1b2c3d4-1111-4aaa-8bbb-000000000005"), FarmerDemoUserId,
                "اس سال اللہ کا کرم ہے — آلو کی پیداوار بہت اچھی رہی۔ آٹھ ایکڑ سے 320 منڈ نکلے اور قیمت بھی اچھی ملی۔ سب کسان بھائیوں کے لیے دعا ہے کہ آپ سب کی فصلیں بھی کامیاب ہوں۔",
                TimeSpan.FromDays(3),
                new[]
                {
                    (
                        new Guid("a1b2c3d4-1111-4aaa-8bbb-000000050001"), TeamUserId,
                        "Mubarak ho! 🎉 Record this sale as income in the Financial Ledger so the season's net profit stays visible.",
                        TimeSpan.FromDays(2)
                    )
                }
            ),
            (
                new Guid("a1b2c3d4-1111-4aaa-8bbb-000000000006"), TeamUserId,
                "Welcome to the Kisan Network! 👋 Share your farming experience, ask questions and help farmers across Pakistan. Every post here is visible to all SABZ farmers — this feed belongs to you.",
                TimeSpan.FromDays(5),
                Array.Empty<(Guid, Guid, string, TimeSpan)>()
            )
        };

        foreach (var (postId, userId, content, age, comments) in seed)
        {
            if (!existingPostIds.Contains(postId))
            {
                db.CommunityPosts.Add(new CommunityPost
                {
                    Id = postId,
                    UserId = userId,
                    Content = content,
                    CreatedAt = now - age
                });
                created.posts++;
            }

            foreach (var (commentId, commentUserId, commentContent, commentAge) in comments)
            {
                if (!existingCommentIds.Contains(commentId))
                {
                    db.CommunityComments.Add(new CommunityComment
                    {
                        Id = commentId,
                        PostId = postId,
                        UserId = commentUserId,
                        Content = commentContent,
                        CreatedAt = now - commentAge
                    });
                    created.comments++;
                }
            }
        }

        if (created.posts > 0 || created.comments > 0)
            await db.SaveChangesAsync();

        return (created.users, created.posts, created.comments);
    }

    private static User NewDemoUser(Guid id, string fullName, string email, IPasswordService passwordService)
    {
        // Unguessable random password - demo accounts exist only to author
        // seed content and are never used for sign-in.
        var randomSecret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hasherTarget = new User { FullName = fullName, PasswordHash = string.Empty };
        return new User
        {
            Id = id,
            FullName = fullName,
            Email = email,
            PhoneNumber = null,
            PasswordHash = passwordService.HashPassword(hasherTarget, randomSecret),
            PreferredLanguage = "English",
            Role = "Farmer",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
    }
}
