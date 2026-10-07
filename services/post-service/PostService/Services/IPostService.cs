using PostService.Models;

namespace PostService.Services
{
    public interface IPostService
    {
        Task<PostResponseDto> CreatePostAsync(CreatePostDto dto, Guid userId, string username);
        Task<PagedResult<PostResponseDto>> GetFeedAsync(int pageNumber = 1, int pageSize = 10);
        Task<PostResponseDto> GetPostAsync(Guid postId);
    }
}
