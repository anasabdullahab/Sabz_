using Microsoft.AspNetCore.Identity;
using SABZ.Application.Interfaces;
using SABZ.Domain.Entities;

namespace SABZ.Infrastructure.Services;

public class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public string HashPassword(User user, string password)
    {
        return _passwordHasher.HashPassword(user, password);
    }

    public bool VerifyPassword(User user, string passwordHash, string password)
    {
        var result = _passwordHasher.VerifyHashedPassword(user, passwordHash, password);
        return result == PasswordVerificationResult.Success
            || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
