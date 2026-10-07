using UserService.Models;

namespace UserService.Services
{
    public interface IUserService
    {
        Task<UserResponseDto> RegisterAsync(CreateUserDto dto);
        Task<LoginResponseDto> LoginAsync(LoginDto dto);
        Task<UserResponseDto> GetUserAsync(Guid userId);
    }
}
