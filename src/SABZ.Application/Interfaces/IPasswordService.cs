using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

public interface IPasswordService
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string passwordHash, string password);
}
