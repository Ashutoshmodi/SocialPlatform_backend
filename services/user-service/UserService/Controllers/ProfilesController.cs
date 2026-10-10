using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ProfilesController : ControllerBase
    {
        private readonly IProfileService _profiles;

        public ProfilesController(IProfileService profiles)
        {
            _profiles = profiles;
        }

        private Guid CurrentUserId =>
            Guid.TryParse(User.FindFirst("sub")?.Value, out var id)
                ? id
                : throw new UnauthorizedAccessException("Token has no user id");

        [HttpGet("me")]
        public Task<ProfileDto> GetMe() =>
            _profiles.GetProfileAsync(CurrentUserId, CurrentUserId);

        [HttpPut("me")]
        public Task<ProfileDto> UpdateMe([FromBody] UpdateProfileDto dto) =>
            _profiles.UpdateProfileAsync(CurrentUserId, dto);

        [HttpGet("{id:guid}")]
        public Task<ProfileDto> GetById(Guid id) =>
            _profiles.GetProfileAsync(id, CurrentUserId);

        [HttpPost("{id:guid}/follow")]
        public async Task<IActionResult> Follow(Guid id)
        {
            await _profiles.FollowAsync(CurrentUserId, id);
            return NoContent();
        }

        [HttpDelete("{id:guid}/follow")]
        public async Task<IActionResult> Unfollow(Guid id)
        {
            await _profiles.UnfollowAsync(CurrentUserId, id);
            return NoContent();
        }

        [HttpGet("{id:guid}/followers")]
        public Task<PagedResult<UserSummaryDto>> Followers(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20) =>
            _profiles.GetFollowersAsync(id, pageNumber, pageSize);

        [HttpGet("{id:guid}/following")]
        public Task<PagedResult<UserSummaryDto>> Following(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20) =>
            _profiles.GetFollowingAsync(id, pageNumber, pageSize);

        [HttpGet("search")]
        public Task<List<UserSummaryDto>> Search([FromQuery] string? q, [FromQuery] int limit = 10) =>
            _profiles.SearchAsync(q, limit);
    }
}
