namespace SABZ.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public required string FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public required string PasswordHash { get; set; }
    public string PreferredLanguage { get; set; } = "English";
    public string Role { get; set; } = "Farmer";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
