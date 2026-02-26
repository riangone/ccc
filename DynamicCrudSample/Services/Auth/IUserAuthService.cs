using DynamicCrudSample.Models.Auth;

namespace DynamicCrudSample.Services.Auth;

public interface IUserAuthService
{
    Task<AppUser?> ValidateCredentialsAsync(string userName, string password);
    Task<IReadOnlyList<AppUser>> GetAllAsync();
    Task<AppUser?> GetByIdAsync(int id);
    Task<int> CreateAsync(UserEditViewModel input);
    Task UpdateAsync(UserEditViewModel input);
}
