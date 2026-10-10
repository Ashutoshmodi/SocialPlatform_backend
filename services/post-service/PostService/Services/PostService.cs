using Microsoft.EntityFrameworkCore;
using PostService.Data;
using PostService.Models;

namespace PostService.Services
{
    public class PostService : IPostService
    {
        private readonly PostDbContext _context;
        private readonly ILogger<PostService> _logger;

        public PostService(PostDbContext context, ILogger<PostService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PostResponseDto> CreatePostAsync(CreatePostDto dto, Guid userId, string username)
        {
            var post = new Post
            {
                Id = Guid.NewGuid(),
                AuthorId = userId,
                AuthorUsername = username,
                Content = dto.Content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Post {PostId} created by {Username}", post.Id, username);
            return Map(post);
        }

        public Task<PagedResult<PostResponseDto>> GetFeedAsync(int pageNumber, int pageSize) =>
            PageAsync(_context.Posts.Where(p => !p.IsDeleted), pageNumber, pageSize);

        public Task<PagedResult<PostResponseDto>> GetUserPostsAsync(Guid userId, int pageNumber, int pageSize) =>
            PageAsync(_context.Posts.Where(p => p.AuthorId == userId && !p.IsDeleted), pageNumber, pageSize);

        public async Task<PostResponseDto> GetPostAsync(Guid postId)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted)
                ?? throw new KeyNotFoundException("Post not found");
            return Map(post);
        }

        public async Task DeletePostAsync(Guid postId, Guid userId)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted)
                ?? throw new KeyNotFoundException("Post not found");

            if (post.AuthorId != userId)
                throw new UnauthorizedAccessException("You can only delete your own posts");

            post.IsDeleted = true;   // soft delete: the row stays for auditing
            await _context.SaveChangesAsync();

            _logger.LogInformation("Post {PostId} deleted by {UserId}", postId, userId);
        }

        private static async Task<PagedResult<PostResponseDto>> PageAsync(IQueryable<Post> query, int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var total = await query.CountAsync();
            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<PostResponseDto>
            {
                Items = posts.Select(Map).ToList(),
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        private static PostResponseDto Map(Post p) => new()
        {
            Id = p.Id,
            AuthorId = p.AuthorId,
            AuthorUsername = p.AuthorUsername,
            Content = p.Content,
            CreatedAt = p.CreatedAt,
            LikeCount = p.LikeCount,
            CommentCount = p.CommentCount
        };
    }
}
