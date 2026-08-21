using SABZ.Domain.Entities;

namespace SABZ.Application.Interfaces;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string email);
    Task<bool> PhoneNumberExistsAsync(string phoneNumber);
    Task<User?> FindByEmailAsync(string email);
    Task<User?> FindByPhoneNumberAsync(string phoneNumber);
    Task<User?> FindByIdAsync(Guid id);
    Task AddAsync(User user);
    Task SaveChangesAsync();
}
