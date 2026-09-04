using SABZ.Application.DTOs.Auth;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;
using SABZ.Domain.Exceptions;

namespace SABZ.Application.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;

    public AuthService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Validate at least one of email or phone
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            throw new ValidationException("At least one of Email or PhoneNumber must be provided.");
        }

        // Check email uniqueness
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            if (await _userRepository.EmailExistsAsync(request.Email))
                throw new ConflictException("A user with this email already exists.");
        }

        // Check phone uniqueness
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            if (await _userRepository.PhoneNumberExistsAsync(request.PhoneNumber))
                throw new ConflictException("A user with this phone number already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber,
            PasswordHash = string.Empty, // will be set below
            PreferredLanguage = string.IsNullOrWhiteSpace(request.PreferredLanguage) ? "English" : request.PreferredLanguage,
            Role = "Farmer",
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordService.HashPassword(user, request.Password);

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        return new AuthResponse
        {
            Success = true,
            Message = "Registration successful.",
            Token = _tokenService.GenerateToken(user),
            User = MapToUserResponse(user)
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        const string genericMessage = "Invalid email/phone or password.";

        var user = await _userRepository.FindByEmailAsync(request.Identifier)
                   ?? await _userRepository.FindByPhoneNumberAsync(request.Identifier);

        if (user is null)
            throw new AuthenticationException(genericMessage);

        if (!_passwordService.VerifyPassword(user, user.PasswordHash, request.Password))
            throw new AuthenticationException(genericMessage);

        var token = _tokenService.GenerateToken(user);

        return new AuthResponse
        {
            Success = true,
            Message = "Login successful.",
            Token = token,
            User = MapToUserResponse(user)
        };
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userRepository.FindByIdAsync(userId)
                   ?? throw new NotFoundException("User not found.");

        return MapToUserResponse(user);
    }

    private static UserResponse MapToUserResponse(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            PreferredLanguage = user.PreferredLanguage,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }
}
