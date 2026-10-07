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
                Content = dto.Content,
                CreatedAt = DateTime.UtcNow
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Post created: {post.Id}");

            return new PostResponseDto
            {
                Id = post.Id,
                AuthorId = post.AuthorId,
                AuthorUsername = username,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                LikeCount = post.LikeCount,
                CommentCount = post.CommentCount
            };
        }

        public async Task<PagedResult<PostResponseDto>> GetFeedAsync(int pageNumber = 1, int pageSize = 10)
        {
            var totalCount = await _context.Posts
                .Where(p => !p.IsDeleted)
                .CountAsync();

            var posts = await _context.Posts
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = posts.Select(p => new PostResponseDto
            {
                Id = p.Id,
                AuthorId = p.AuthorId,
                Content = p.Content,
                CreatedAt = p.CreatedAt,
                LikeCount = p.LikeCount,
                CommentCount = p.CommentCount
            }).ToList();

            return new PagedResult<PostResponseDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PostResponseDto> GetPostAsync(Guid postId)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted);

            if (post == null)
                throw new KeyNotFoundException("Post not found");

            return new PostResponseDto
            {
                Id = post.Id,
                AuthorId = post.AuthorId,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                LikeCount = post.LikeCount,
                CommentCount = post.CommentCount
            };
        }
    }
}
