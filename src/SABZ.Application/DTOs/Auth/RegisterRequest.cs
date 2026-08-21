using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters.")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required.")]
    [Compare("Password", ErrorMessage = "Password and confirm password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? PreferredLanguage { get; set; }
}
