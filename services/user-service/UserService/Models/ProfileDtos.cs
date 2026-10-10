using System.ComponentModel.DataAnnotations;

namespace UserService.Models
{
    public class ProfileDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = "";
        public string? Bio { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }

        // Depend on who is looking, so they are filled in per request (never cached)
        public bool IsFollowing { get; set; }
        public bool IsMe { get; set; }
        public string? Email { get; set; }   // only returned on your own profile
    }

    public class UserSummaryDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = "";
        public string? Bio { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }

    public class UpdateProfileDto
    {
        [MaxLength(500, ErrorMessage = "Bio can be at most 500 characters")]
        public string? Bio { get; set; }

        [MaxLength(500)]
        public string? ProfilePictureUrl { get; set; }
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}