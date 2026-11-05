using Microsoft.EntityFrameworkCore;
using PostService.Data;
using PostService.Models;

namespace PostService.Services
{
    public class PostService : IPostService
    {
        private readonly PostDbContext _context;
        private readonly IUserServiceClient _users;
        private readonly ILogger<PostService> _logger;

        public PostService(PostDbContext context, IUserServiceClient users, ILogger<PostService> logger)
        {
            _context = context;
            _users = users;
            _logger = logger;
        }

        // ---------- Posts ----------

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

        public Task<PagedResult<PostResponseDto>> GetFeedAsync(int pageNumber, int pageSize, Guid? viewerId) =>
            PageAsync(_context.Posts.Where(p => !p.IsDeleted), pageNumber, pageSize, viewerId);

        public async Task<PagedResult<PostResponseDto>> GetFollowingFeedAsync(Guid userId, int pageNumber, int pageSize, CancellationToken ct)
        {
            // 1. Ask UserService who this user follows (throws DependencyUnavailableException if it's down)
            var authorIds = await _users.GetFollowingIdsAsync(userId, ct);

            // 2. Include the user's own posts
            authorIds.Add(userId);

            // 3. One query: posts by any of those authors, newest first
            //    (Npgsql turns Contains into: WHERE "AuthorId" = ANY(@ids))
            var query = _context.Posts.Where(p => !p.IsDeleted && authorIds.Contains(p.AuthorId));
            return await PageAsync(query, pageNumber, pageSize, userId);
        }

        public Task<PagedResult<PostResponseDto>> GetUserPostsAsync(Guid userId, int pageNumber, int pageSize, Guid? viewerId) =>
            PageAsync(_context.Posts.Where(p => p.AuthorId == userId && !p.IsDeleted), pageNumber, pageSize, viewerId);

        public async Task<PostResponseDto> GetPostAsync(Guid postId, Guid? viewerId)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted)
                ?? throw new KeyNotFoundException("Post not found");

            var dto = Map(post);
            await FillLikedByMeAsync(new List<PostResponseDto> { dto }, viewerId);
            return dto;
        }

        public async Task DeletePostAsync(Guid postId, Guid userId)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted)
                ?? throw new KeyNotFoundException("Post not found");

            if (post.AuthorId != userId)
                throw new UnauthorizedAccessException("You can only delete your own posts");

            post.IsDeleted = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Post {PostId} deleted by {UserId}", postId, userId);
        }

        // ---------- Likes ----------

        public async Task<LikeResultDto> LikeAsync(Guid postId, Guid userId)
        {
            await EnsurePostExistsAsync(postId);

            await using var tx = await _context.Database.BeginTransactionAsync();

            // Atomic "insert if not already there". If the like exists, 0 rows are inserted.
            // No check-then-insert race, even with many parallel requests.
            var inserted = await _context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PostLikes" ("PostId", "UserId", "CreatedAt")
                VALUES ({postId}, {userId}, {DateTime.UtcNow})
                ON CONFLICT DO NOTHING
                """);

            if (inserted == 1)
            {
                // Atomic increment in the database: UPDATE ... SET "LikeCount" = "LikeCount" + 1
                await _context.Posts
                    .Where(p => p.Id == postId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.LikeCount, p => p.LikeCount + 1));
            }

            await tx.CommitAsync();
            return await LikeResultAsync(postId, likedByMe: true);
        }

        public async Task<LikeResultDto> UnlikeAsync(Guid postId, Guid userId)
        {
            await EnsurePostExistsAsync(postId);

            await using var tx = await _context.Database.BeginTransactionAsync();

            var deleted = await _context.PostLikes
                .Where(l => l.PostId == postId && l.UserId == userId)
                .ExecuteDeleteAsync();

            if (deleted == 1)
            {
                await _context.Posts
                    .Where(p => p.Id == postId && p.LikeCount > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.LikeCount, p => p.LikeCount - 1));
            }

            await tx.CommitAsync();
            return await LikeResultAsync(postId, likedByMe: false);
        }

        // ---------- Helpers ----------

        private async Task EnsurePostExistsAsync(Guid postId)
        {
            if (!await _context.Posts.AnyAsync(p => p.Id == postId && !p.IsDeleted))
                throw new KeyNotFoundException("Post not found");
        }

        private async Task<LikeResultDto> LikeResultAsync(Guid postId, bool likedByMe)
        {
            var count = await _context.Posts
                .Where(p => p.Id == postId)
                .Select(p => p.LikeCount)
                .FirstAsync();

            return new LikeResultDto { PostId = postId, LikeCount = count, LikedByMe = likedByMe };
        }

        private async Task<PagedResult<PostResponseDto>> PageAsync(IQueryable<Post> query, int pageNumber, int pageSize, Guid? viewerId)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var total = await query.CountAsync();
            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = posts.Select(Map).ToList();
            await FillLikedByMeAsync(items, viewerId);

            return new PagedResult<PostResponseDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // One extra query per page (not per post): "which of these post ids did the viewer like?"
        private async Task FillLikedByMeAsync(List<PostResponseDto> posts, Guid? viewerId)
        {
            if (viewerId is null || posts.Count == 0) return;

            var postIds = posts.Select(p => p.Id).ToList();
            var likedIds = await _context.PostLikes
                .Where(l => l.UserId == viewerId.Value && postIds.Contains(l.PostId))
                .Select(l => l.PostId)
                .ToListAsync();

            var liked = likedIds.ToHashSet();
            foreach (var post in posts)
                post.LikedByMe = liked.Contains(post.Id);
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