namespace PostService.Models
{
    public class PostResponseDto
    {
        public Guid Id { get; set; }
        public Guid AuthorId { get; set; }
        public string AuthorUsername { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool LikedByMe { get; set; }
    }

    public class PostLike
    {
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class LikeResultDto
    {
        public Guid PostId { get; set; }
        public int LikeCount { get; set; }
        public bool LikedByMe { get; set; }
    }
}
