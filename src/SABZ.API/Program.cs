using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SABZ.API.Middleware;
using SABZ.Infrastructure;
using SABZ.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

// Disease detection (Prompt 6): allow multipart image uploads up to ~11 MB
// (image limit is 10 MB + multipart overhead). Oversized bodies are rejected
// by Kestrel before reaching the controller; file-level checks also apply.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 11_000_000;
    options.ValueLengthLimit = int.MaxValue;
});
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 11_000_000;
});

// Add infrastructure (DbContext, repositories, services)
builder.Services.AddInfrastructure(builder.Configuration);

// Configure JWT authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SABZ API",
        Version = "v1",
        Description = "Smart Agriculture Backend API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(doc =>
    {
        var requirement = new OpenApiSecurityRequirement();
        var scheme = new OpenApiSecuritySchemeReference("Bearer", doc);
        requirement.Add(scheme, new List<string>());
        return requirement;
    });
});

var app = builder.Build();

// Apply migrations and seed location data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SabzDbContext>();
    db.Database.Migrate();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await LocationDataSeeder.SeedAsync(db, logger);
    // Backfill crop catalog links, regenerate monitoring checks/notifications
    // and seed demo community content (idempotent, failure never blocks startup).
    await DemoDataSeeder.SeedAsync(db, scope.ServiceProvider, logger);
}

// Use global exception middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger for all environments during development
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
