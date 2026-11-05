using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UserService.Infrastructure;
using UserService.Services;

namespace UserService.Controllers
{
    [ApiController]
    [Route("internal/users")]
    [InternalApiKey]
    [ApiExplorerSettings(IgnoreApi = true)] // hide from Swagger
    public class InternalController : ControllerBase
    {
        private readonly IProfileService _profiles;

        public InternalController(IProfileService profiles)
        {
            _profiles = profiles;
        }

        [HttpGet("{id:guid}/following-ids")]
        public Task<List<Guid>> FollowingIds(Guid id) => _profiles.GetFollowingIdsAsync(id);
    }
}
