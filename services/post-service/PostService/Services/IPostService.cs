using PostService.Models;

namespace PostService.Services
{
    public interface IPostService
    {
        Task<PostResponseDto> CreatePostAsync(CreatePostDto dto, Guid userId, string username);
        Task<PagedResult<PostResponseDto>> GetFeedAsync(int pageNumber, int pageSize, Guid? viewerId);
        Task<PagedResult<PostResponseDto>> GetFollowingFeedAsync(Guid userId, int pageNumber, int pageSize, CancellationToken ct);
        Task<PagedResult<PostResponseDto>> GetUserPostsAsync(Guid userId, int pageNumber, int pageSize, Guid? viewerId);
        Task<PostResponseDto> GetPostAsync(Guid postId, Guid? viewerId);
        Task DeletePostAsync(Guid postId, Guid userId);
        Task<LikeResultDto> LikeAsync(Guid postId, Guid userId);
        Task<LikeResultDto> UnlikeAsync(Guid postId, Guid userId);
    }
}
