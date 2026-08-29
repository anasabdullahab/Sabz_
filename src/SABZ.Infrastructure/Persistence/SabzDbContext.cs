using Microsoft.EntityFrameworkCore;
using SABZ.Domain.Entities;

namespace SABZ.Infrastructure.Persistence;

public class SabzDbContext : DbContext
{
    public SabzDbContext(DbContextOptions<SabzDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Tehsil> Tehsils => Set<Tehsil>();
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<CropCatalog> CropCatalog => Set<CropCatalog>();
    public DbSet<Crop> Crops => Set<Crop>();
    public DbSet<CropRequirement> CropRequirements => Set<CropRequirement>();
    public DbSet<RegionalCropSuitability> RegionalCropSuitabilities => Set<RegionalCropSuitability>();
    public DbSet<CropChangeRule> CropChangeRules => Set<CropChangeRule>();
    public DbSet<DiseaseInformation> DiseaseInformations => Set<DiseaseInformation>();
    public DbSet<CropMonitoringRule> CropMonitoringRules => Set<CropMonitoringRule>();
    public DbSet<CropMonitoringCheck> CropMonitoringChecks => Set<CropMonitoringCheck>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<CommunityPost> CommunityPosts => Set<CommunityPost>();
    public DbSet<CommunityComment> CommunityComments => Set<CommunityComment>();
    public DbSet<MarketplaceListing> MarketplaceListings => Set<MarketplaceListing>();
    public DbSet<MarketplaceConversation> MarketplaceConversations => Set<MarketplaceConversation>();
    public DbSet<MarketplaceMessage> MarketplaceMessages => Set<MarketplaceMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- User ---
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(200);
            entity.Property(u => u.Email).HasMaxLength(320);
            entity.HasIndex(u => u.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
            entity.Property(u => u.PhoneNumber).HasMaxLength(20);
            entity.HasIndex(u => u.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL");
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.PreferredLanguage).IsRequired().HasMaxLength(50).HasDefaultValue("English");
            entity.Property(u => u.Role).IsRequired().HasMaxLength(50).HasDefaultValue("Farmer");
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // --- Province ---
        modelBuilder.Entity<Province>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.NameUrdu).HasMaxLength(200);
            entity.HasMany(p => p.Districts).WithOne(d => d.Province).HasForeignKey(d => d.ProvinceId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- District ---
        modelBuilder.Entity<District>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).IsRequired().HasMaxLength(200);
            entity.Property(d => d.NameUrdu).HasMaxLength(200);
            entity.HasIndex(d => d.ProvinceId);
            entity.HasMany(d => d.Tehsils).WithOne(t => t.District).HasForeignKey(t => t.DistrictId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- Tehsil ---
        modelBuilder.Entity<Tehsil>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(200);
            entity.Property(t => t.NameUrdu).HasMaxLength(200);
            entity.HasIndex(t => t.DistrictId);
        });

        // --- Farm ---
        modelBuilder.Entity<Farm>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.FarmName).IsRequired().HasMaxLength(300);
            entity.Property(f => f.FarmSize).HasPrecision(18, 4);
            entity.Property(f => f.FarmSizeUnit).IsRequired().HasMaxLength(50);
            entity.Property(f => f.Latitude).HasPrecision(18, 10);
            entity.Property(f => f.Longitude).HasPrecision(18, 10);
            entity.Property(f => f.SoilType).HasMaxLength(100);
            entity.Property(f => f.IrrigationType).HasMaxLength(100);
            entity.HasIndex(f => f.UserId);
            entity.HasIndex(f => f.ProvinceId);
            entity.HasIndex(f => f.DistrictId);
            entity.HasIndex(f => f.TehsilId);
            entity.HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(f => f.Province).WithMany().HasForeignKey(f => f.ProvinceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(f => f.District).WithMany().HasForeignKey(f => f.DistrictId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(f => f.Tehsil).WithMany().HasForeignKey(f => f.TehsilId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(f => f.Crops).WithOne(c => c.Farm).HasForeignKey(c => c.FarmId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- CropCatalog ---
        modelBuilder.Entity<CropCatalog>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
            entity.Property(c => c.ScientificName).HasMaxLength(300);
            entity.Property(c => c.Category).HasMaxLength(100);
            entity.Property(c => c.Description).HasMaxLength(1000);
        });

        // --- Crop (farmer's planted crop) ---
        modelBuilder.Entity<Crop>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CropName).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Season).IsRequired().HasMaxLength(50);
            entity.Property(c => c.GrowthStage).HasMaxLength(100);
            entity.Property(c => c.PreviousCrop).HasMaxLength(200);
            entity.Property(c => c.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Active");
            entity.HasIndex(c => c.FarmId);
            entity.HasIndex(c => c.CropCatalogId);
            entity.HasOne(c => c.CropCatalog).WithMany().HasForeignKey(c => c.CropCatalogId).OnDelete(DeleteBehavior.SetNull);
        });

        // --- RegionalCropSuitability ---
        modelBuilder.Entity<RegionalCropSuitability>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Season).IsRequired().HasMaxLength(50);
            entity.Property(r => r.SuitabilityLevel).IsRequired().HasMaxLength(50);
            entity.Property(r => r.Notes).HasMaxLength(1000);
            entity.Property(r => r.Source).HasMaxLength(500);
            entity.HasIndex(r => r.ProvinceId);
            entity.HasIndex(r => r.DistrictId);
            entity.HasIndex(r => r.TehsilId);
            entity.HasIndex(r => r.CropCatalogId);
            entity.HasOne(r => r.Province).WithMany().HasForeignKey(r => r.ProvinceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.District).WithMany().HasForeignKey(r => r.DistrictId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Tehsil).WithMany().HasForeignKey(r => r.TehsilId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.CropCatalog).WithMany(c => c.RegionalSuitabilities).HasForeignKey(r => r.CropCatalogId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- CropRequirement (data-driven suitability requirements) ---
        modelBuilder.Entity<CropRequirement>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Season).IsRequired().HasMaxLength(50);
            entity.Property(r => r.WaterRequirement).IsRequired().HasMaxLength(20);
            entity.Property(r => r.MinTempC).HasPrecision(5, 2);
            entity.Property(r => r.MaxTempC).HasPrecision(5, 2);
            entity.Property(r => r.SuitableSoils).HasMaxLength(500);
            entity.Property(r => r.Source).HasMaxLength(500);
            entity.HasIndex(r => r.CropCatalogId);
            entity.HasOne(r => r.CropCatalog).WithMany().HasForeignKey(r => r.CropCatalogId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- CropChangeRule (data-driven crop-change/rotation guidance) ---
        modelBuilder.Entity<CropChangeRule>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.PreviousCategory).IsRequired().HasMaxLength(100);
            entity.Property(r => r.NextCategory).IsRequired().HasMaxLength(100);
            entity.Property(r => r.Effect).IsRequired().HasMaxLength(20);
            entity.Property(r => r.Explanation).IsRequired().HasMaxLength(500);
            entity.Property(r => r.Source).HasMaxLength(500);
            entity.HasIndex(r => new { r.PreviousCategory, r.NextCategory }).IsUnique();
        });

        // --- DiseaseInformation (Prompt 6: curated agricultural guidance) ---
        modelBuilder.Entity<DiseaseInformation>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.DiseaseName).IsRequired().HasMaxLength(150);
            entity.Property(d => d.Description).IsRequired().HasMaxLength(1000);
            entity.Property(d => d.Symptoms).IsRequired().HasMaxLength(1000);
            entity.Property(d => d.RecommendedActions).IsRequired().HasMaxLength(1500);
            entity.Property(d => d.Prevention).IsRequired().HasMaxLength(1500);
            entity.Property(d => d.Monitoring).IsRequired().HasMaxLength(1500);
            entity.Property(d => d.Source).IsRequired().HasMaxLength(500);
            entity.HasIndex(d => d.CropCatalogId);
            entity.HasOne(d => d.CropCatalog)
                .WithMany()
                .HasForeignKey(d => d.CropCatalogId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // --- CropMonitoringRule (Prompt 7: data-driven monitoring checkpoints) ---
        modelBuilder.Entity<CropMonitoringRule>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Title).IsRequired().HasMaxLength(200);
            entity.Property(r => r.Description).IsRequired().HasMaxLength(1000);
            entity.Property(r => r.InspectionItems).IsRequired().HasMaxLength(1500);
            entity.Property(r => r.Priority).IsRequired().HasMaxLength(20);
            entity.Property(r => r.TriggerType).IsRequired().HasMaxLength(30);
            entity.Property(r => r.Source).IsRequired().HasMaxLength(500);
            entity.HasIndex(r => r.CropCatalogId);
            entity.HasOne(r => r.CropCatalog)
                .WithMany()
                .HasForeignKey(r => r.CropCatalogId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // --- CropMonitoringCheck (Prompt 7: scheduled checks per crop) ---
        modelBuilder.Entity<CropMonitoringCheck>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(c => c.Observation).HasConversion<string>().HasMaxLength(30);
            entity.Property(c => c.Title).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Description).IsRequired().HasMaxLength(1000);
            entity.Property(c => c.InspectionItems).IsRequired().HasMaxLength(1500);
            entity.Property(c => c.Priority).IsRequired().HasMaxLength(20);
            entity.Property(c => c.FarmerNotes).HasMaxLength(1000);

            // Idempotent generation: at most one check per (crop, rule).
            entity.HasIndex(c => new { c.CropId, c.RuleId }).IsUnique();
            entity.HasIndex(c => c.FarmId);
            entity.HasIndex(c => c.ScheduledDate);

            entity.HasOne(c => c.Crop)
                .WithMany()
                .HasForeignKey(c => c.CropId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(c => c.Rule)
                .WithMany()
                .HasForeignKey(c => c.RuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // --- Notification (Prompt 8: central in-app notifications) ---
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Title).IsRequired().HasMaxLength(200);
            entity.Property(n => n.Message).IsRequired().HasMaxLength(1000);
            entity.Property(n => n.Category).IsRequired().HasMaxLength(50);
            entity.Property(n => n.ReferenceType).IsRequired().HasMaxLength(50).HasDefaultValue("None");
            entity.Property(n => n.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Read-path performance.
            entity.HasIndex(n => new { n.UserId, n.CreatedAt });
            entity.HasIndex(n => new { n.UserId, n.IsRead });

            // Database-level duplicate prevention: at most one notification per
            // (user, category, referenced entity). ReferenceType/ReferenceId are
            // non-nullable so the unique index behaves deterministically.
            entity.HasIndex(n => new { n.UserId, n.Category, n.ReferenceType, n.ReferenceId }).IsUnique();

            entity.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- FinancialTransaction (Prompt 9: farm P&L ledger) ---
        modelBuilder.Entity<FinancialTransaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TransactionType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(t => t.Category).IsRequired().HasMaxLength(100);
            // Money: decimal only (never float/double), PKR with 2 decimals.
            entity.Property(t => t.Amount).HasPrecision(18, 2);
            entity.Property(t => t.Notes).HasMaxLength(1000);
            entity.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // P&L read paths: date-range summaries, per-type totals, crop-level.
            entity.HasIndex(t => new { t.FarmId, t.TransactionDate });
            entity.HasIndex(t => new { t.FarmId, t.TransactionType });
            entity.HasIndex(t => t.CropId);

            // Farm deletion removes its ledger (single cascade path). The
            // CropId FK must be Restrict in the database because SQL Server
            // rejects a second cascading path (Farms -> Crops -> here, error
            // 1785); the SetNull semantics for crop deletion are enforced in
            // the application layer (CropService + NullifyCropReferencesAsync).
            entity.HasOne(t => t.Farm)
                .WithMany()
                .HasForeignKey(t => t.FarmId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(t => t.Crop)
                .WithMany()
                .HasForeignKey(t => t.CropId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- CommunityPost (Prompt 14: farmer community feed) ---
        modelBuilder.Entity<CommunityPost>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Content).IsRequired().HasMaxLength(2000);
            // Safe image reference only (absolute HTTP/HTTPS URL, enforced in
            // the application layer); never a filesystem path or binary blob.
            entity.Property(p => p.ImageUrl).HasMaxLength(2048);
            entity.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Community feed read paths: newest-first per-author and global
            // listings, both DB-side paginated.
            entity.HasIndex(p => new { p.UserId, p.CreatedAt });
            entity.HasIndex(p => p.CreatedAt);

            // User deletion is Restrict in the database (community content is
            // preserved rather than silently destroyed); SQL Server would also
            // reject a second cascading path through Comments -> UserId.
            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(p => p.Comments)
                .WithOne(c => c.Post)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- CommunityComment (Prompt 14: discussion on community posts) ---
        modelBuilder.Entity<CommunityComment>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Content).IsRequired().HasMaxLength(1000);
            entity.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Oldest-first comment threads plus comment counting, DB-side.
            entity.HasIndex(c => new { c.PostId, c.CreatedAt });
            entity.HasIndex(c => new { c.UserId, c.CreatedAt });

            // Single cascade path: Post deletion removes its comments (also
            // soft-deleted in the application layer). UserId is Restrict to
            // avoid a second cascading path (SQL Server error 1785).
            entity.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- MarketplaceListing (Prompt 15: farmer-to-farmer discovery) ---
        modelBuilder.Entity<MarketplaceListing>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Title).IsRequired().HasMaxLength(150);
            entity.Property(l => l.Category).IsRequired().HasMaxLength(50);
            entity.Property(l => l.ListingType).IsRequired().HasMaxLength(10);
            entity.Property(l => l.Description).IsRequired().HasMaxLength(2000);
            // Informational asking/rental rate only - fixed precision money,
            // never floating point, never charged or processed by SABZ.
            entity.Property(l => l.Price).HasPrecision(18, 2);
            entity.Property(l => l.PriceUnit).IsRequired().HasMaxLength(20);
            entity.Property(l => l.Location).IsRequired().HasMaxLength(200);
            entity.Property(l => l.ContactNumber).IsRequired().HasMaxLength(30);
            entity.Property(l => l.Condition).IsRequired().HasMaxLength(10);
            entity.Property(l => l.Availability).IsRequired().HasMaxLength(100);
            // Safe image reference only (absolute HTTP/HTTPS URL, enforced in
            // the application layer); never a filesystem path or binary blob.
            entity.Property(l => l.ImageUrl).HasMaxLength(2048);
            entity.Property(l => l.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Marketplace read paths: newest-first feed, per-seller listings
            // and controlled-value filters, all DB-side paginated.
            entity.HasIndex(l => new { l.UserId, l.CreatedAt });
            entity.HasIndex(l => l.CreatedAt);
            entity.HasIndex(l => l.Category);
            entity.HasIndex(l => l.ListingType);

            // User deletion is Restrict (marketplace history is preserved);
            // a soft-deleted listing must never destroy private inbox history,
            // so Listing -> Conversations is Restrict as well.
            entity.HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(l => l.Conversations)
                .WithOne(c => c.Listing)
                .HasForeignKey(c => c.ListingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- MarketplaceConversation (Prompt 15: private inbox thread) ---
        modelBuilder.Entity<MarketplaceConversation>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(c => c.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Inbox read paths: newest-first per participant, plus lookup by
            // listing. The unique identity guarantees one conversation per
            // listing/buyer/seller trio ("Message Seller" reuses it).
            entity.HasIndex(c => new { c.BuyerUserId, c.UpdatedAt });
            entity.HasIndex(c => new { c.SellerUserId, c.UpdatedAt });
            entity.HasIndex(c => c.ListingId);
            entity.HasIndex(c => new { c.ListingId, c.BuyerUserId, c.SellerUserId })
                .IsUnique();

            // All user relationships are Restrict to avoid multiple cascade
            // paths (SQL Server error 1785); the only cascading edge in the
            // marketplace graph is Conversation -> Messages.
            entity.HasOne(c => c.BuyerUser)
                .WithMany()
                .HasForeignKey(c => c.BuyerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.SellerUser)
                .WithMany()
                .HasForeignKey(c => c.SellerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- MarketplaceMessage (Prompt 15: private message) ---
        modelBuilder.Entity<MarketplaceMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Content).IsRequired().HasMaxLength(2000);
            entity.Property(m => m.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Oldest-first message threads per conversation, DB-side.
            entity.HasIndex(m => new { m.ConversationId, m.CreatedAt });

            // Sender is Restrict (single cascade path stays Conversation ->
            // Messages); sender identity always comes from the JWT.
            entity.HasOne(m => m.SenderUser)
                .WithMany()
                .HasForeignKey(m => m.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- Seed Data ---
        SeedData.Apply(modelBuilder);
    }
}
