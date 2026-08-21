using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(User user);
}
