using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using System.Text.Json;
using UserService.Data;
using UserService.Models;

namespace UserService.Services
{
    public class ProfileService : IProfileService
    {

        private static readonly DistributedCacheEntryOptions cacheOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        };

        private readonly UserDbContext _db;
        private readonly IDistributedCache _cache;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(UserDbContext db, IDistributedCache cache, ILogger<ProfileService> logger)
        {
            _db = db;
            _cache = cache;
            _logger = logger;
        }

        private static string CacheKey(Guid userId) => $"profile:{userId}";

        // ---------- Profiles ----------

        public async Task<ProfileDto> GetProfileAsync(Guid userId, Guid viewerId)
        {
            // Cache-aside: try Redis first, fall back to the database and fill the cache
            var profile = await GetCachedAsync(userId) ?? await LoadAndCacheAsync(userId);

            profile.IsMe = userId == viewerId;
            if (!profile.IsMe)
            {
                profile.Email = null;
                profile.IsFollowing = await _db.Follows
                    .AnyAsync(f => f.FollowerId == viewerId && f.FolloweeId == userId);
            }
            return profile;
        }

        public async Task<ProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
        {
            var user = await _db.Users.FindAsync(userId)
                ?? throw new KeyNotFoundException("User not found");

            var pictureUrl = string.IsNullOrWhiteSpace(dto.ProfilePictureUrl) ? null : dto.ProfilePictureUrl.Trim();
            if (pictureUrl != null && !(Uri.TryCreate(pictureUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            {
                throw new ArgumentException("Profile picture must be an http:// or https:// URL");
            }

            user.Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();
            user.ProfilePictureUrl = pictureUrl;
            await _db.SaveChangesAsync();

            await InvalidateAsync(userId);
            return await GetProfileAsync(userId, userId);
        }

        // ---------- Follow / unfollow ----------

        public async Task FollowAsync(Guid followerId, Guid followeeId)
        {
            if (followerId == followeeId)
                throw new ArgumentException("You can't follow yourself");

            if (!await _db.Users.AnyAsync(u => u.Id == followeeId && u.IsActive))
                throw new KeyNotFoundException("User not found");

            // Idempotent: following someone you already follow is a no-op
            if (await _db.Follows.AnyAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId))
                return;

            _db.Follows.Add(new Follow
            {
                FollowerId = followerId,
                FolloweeId = followeeId,
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two requests raced (e.g. double click): the row already exists, which is fine
                return;
            }

            _logger.LogInformation("{FollowerId} followed {FolloweeId}", followerId, followeeId);
            await InvalidateAsync(followerId, followeeId);   // both users' counts changed
        }

        public async Task UnfollowAsync(Guid followerId, Guid followeeId)
        {
            var deleted = await _db.Follows
                .Where(f => f.FollowerId == followerId && f.FolloweeId == followeeId)
                .ExecuteDeleteAsync();

            if (deleted > 0)
            {
                _logger.LogInformation("{FollowerId} unfollowed {FolloweeId}", followerId, followeeId);
                await InvalidateAsync(followerId, followeeId);
            }
        }

        // ---------- Lists and search ----------

        public Task<PagedResult<UserSummaryDto>> GetFollowersAsync(Guid userId, int pageNumber, int pageSize)
        {
            var query = from f in _db.Follows
                        join u in _db.Users on f.FollowerId equals u.Id
                        where f.FolloweeId == userId
                        orderby f.CreatedAt descending
                        select u;
            return PageAsync(query, pageNumber, pageSize);
        }

        public Task<PagedResult<UserSummaryDto>> GetFollowingAsync(Guid userId, int pageNumber, int pageSize)
        {
            var query = from f in _db.Follows
                        join u in _db.Users on f.FolloweeId equals u.Id
                        where f.FollowerId == userId
                        orderby f.CreatedAt descending
                        select u;
            return PageAsync(query, pageNumber, pageSize);
        }

        public async Task<List<UserSummaryDto>> SearchAsync(string? query, int limit)
        {
            query = query?.Trim() ?? "";
            if (query.Length < 2) return new List<UserSummaryDto>();

            // Escape LIKE wildcards so a search for "a_b" means literally "a_b"
            var escaped = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

            return await _db.Users
                .Where(u => u.IsActive && EF.Functions.ILike(u.Username, $"%{escaped}%"))
                .OrderBy(u => u.Username.Length)       // closest matches first
                .ThenBy(u => u.Username)
                .Take(Math.Clamp(limit, 1, 20))
                .Select(u => new UserSummaryDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Bio = u.Bio,
                    ProfilePictureUrl = u.ProfilePictureUrl
                })
                .ToListAsync();
        }

        private static async Task<PagedResult<UserSummaryDto>> PageAsync(IQueryable<User> query, int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var total = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserSummaryDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Bio = u.Bio,
                    ProfilePictureUrl = u.ProfilePictureUrl
                })
                .ToListAsync();

            return new PagedResult<UserSummaryDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // ---------- Cache helpers ----------
        // Every Redis call is wrapped: if Redis is down we log a warning and use the database.

        private async Task<ProfileDto> LoadAndCacheAsync(Guid userId)
        {
            _logger.LogInformation("Profile cache MISS for {UserId}, loading from database", userId);

            var profile = await _db.Users
                .Where(u => u.Id == userId && u.IsActive)
                .Select(u => new ProfileDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    Bio = u.Bio,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    CreatedAt = u.CreatedAt,
                    FollowersCount = _db.Follows.Count(f => f.FolloweeId == u.Id),
                    FollowingCount = _db.Follows.Count(f => f.FollowerId == u.Id)
                })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User not found");

            try
            {
                await _cache.SetStringAsync(CacheKey(userId), JsonSerializer.Serialize(profile), cacheOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis write failed for {UserId}", userId);
            }

            return profile;
        }

        private async Task<ProfileDto?> GetCachedAsync(Guid userId)
        {
            try
            {
                var json = await _cache.GetStringAsync(CacheKey(userId));
                if (json is null) return null;

                _logger.LogInformation("Profile cache HIT for {UserId}", userId);
                return JsonSerializer.Deserialize<ProfileDto>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis read failed for {UserId}, using database", userId);
                return null;
            }
        }

        private async Task InvalidateAsync(params Guid[] userIds)
        {
            foreach (var id in userIds)
            {
                try
                {
                    await _cache.RemoveAsync(CacheKey(id));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Redis invalidation failed for {UserId}", id);
                }
            }
        }

    }
}
