namespace UserService.Models
{
    // One row = "FollowerId follows FolloweeId"
    public class Follow
    {
        public Guid FollowerId { get; set; }
        public Guid FolloweeId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}