using PostService.Models;

namespace PostService.Services
{
    public interface IPostService
    {
        Task<PostResponseDto> CreatePostAsync(CreatePostDto dto, Guid userId, string username);
        Task<PagedResult<PostResponseDto>> GetFeedAsync(int pageNumber, int pageSize);
        Task<PagedResult<PostResponseDto>> GetUserPostsAsync(Guid userId, int pageNumber, int pageSize);
        Task<PostResponseDto> GetPostAsync(Guid postId);
        Task DeletePostAsync(Guid postId, Guid userId);
    }
}
