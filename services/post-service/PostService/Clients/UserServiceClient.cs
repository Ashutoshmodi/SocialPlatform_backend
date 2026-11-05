using PostService.Services;
using static System.Net.WebRequestMethods;

namespace PostService.Clients
{
    public class UserServiceClient : IUserServiceClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<UserServiceClient> _logger;
        // Thrown when another service we depend on can't be reached. Mapped to HTTP 503.
        public class DependencyUnavailableException : Exception
        {
            public DependencyUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
        }
        public UserServiceClient(HttpClient http, ILogger<UserServiceClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<List<Guid>> GetFollowingIdsAsync(Guid userId, CancellationToken ct = default)
        {
            try
            {
                var ids = await _http.GetFromJsonAsync<List<Guid>>($"internal/users/{userId}/following-ids", ct);
                return ids ?? new List<Guid>();
            }
            catch (Exception ex)
            {
                // HttpRequestException: UserService down, or returned 4xx/5xx (e.g. wrong API key)
                // TaskCanceledException: request exceeded the timeout
                _logger.LogWarning(ex, "UserService unavailable while loading following ids for {UserId}", userId);
                throw new DependencyUnavailableException(
                    "The Following feed is temporarily unavailable. Try the Everyone tab.", ex);
            }
        }
    }
}
