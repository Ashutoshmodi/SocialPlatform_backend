namespace PostService.Models
{
    public class PostResponseDto
    {
        public Guid Id { get; set; }
        public Guid AuthorId { get; set; }
        public string AuthorUsername { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
    }
}
