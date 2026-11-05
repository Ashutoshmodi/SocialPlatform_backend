namespace PostService.Services
{
    public interface IUserServiceClient
    {
        Task<List<Guid>> GetFollowingIdsAsync(Guid userId, CancellationToken ct = default);
    }
}
