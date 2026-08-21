using System.ComponentModel.DataAnnotations;

namespace SABZ.Application.DTOs.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "Identifier (email or phone number) is required.")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}
