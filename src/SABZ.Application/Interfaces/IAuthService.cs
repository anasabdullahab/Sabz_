using SABZ.Application.DTOs.Auth;

namespace SABZ.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserResponse> GetCurrentUserAsync(Guid userId);
}
