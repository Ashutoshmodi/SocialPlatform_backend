using UserService.Models;

namespace UserService.Services
{
    public interface IProfileService
    {
        Task<ProfileDto> GetProfileAsync(Guid userId, Guid viewerId);
        Task<ProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);
        Task FollowAsync(Guid followerId, Guid followeeId);
        Task UnfollowAsync(Guid followerId, Guid followeeId);
        Task<PagedResult<UserSummaryDto>> GetFollowersAsync(Guid userId, int pageNumber, int pageSize);
        Task<PagedResult<UserSummaryDto>> GetFollowingAsync(Guid userId, int pageNumber, int pageSize);
        Task<List<UserSummaryDto>> SearchAsync(string? query, int limit);
    }
}
