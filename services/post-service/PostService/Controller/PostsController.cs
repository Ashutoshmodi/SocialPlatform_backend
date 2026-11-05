using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using PostService.Models;
using PostService.Services;

namespace PostService.Controllers
{
    [ApiController]
    [Route("api/posts")]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _posts;

        public PostsController(IPostService posts)
        {
            _posts = posts;
        }

        private Guid CurrentUserId => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : throw new UnauthorizedAccessException("Token has no user id");

        private string CurrentUsername => User.FindFirst("unique_name")?.Value ?? "unknown";

        // Optional: public endpoints still read the user if a valid token was sent(for likedByMe)
        private Guid? ViewerId => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<PostResponseDto>> Create([FromBody] CreatePostDto dto)
        {
            var post = await _posts.CreatePostAsync(dto, CurrentUserId, CurrentUsername);
            return CreatedAtAction(nameof(GetById), new { id = post.Id }, post);
        }

        [HttpGet("feed")]
        public Task<PagedResult<PostResponseDto>> Feed([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10) =>
            _posts.GetFeedAsync(pageNumber, pageSize, ViewerId);

        [Authorize]
        [HttpGet("feed/following")]
        public Task<PagedResult<PostResponseDto>> FollowingFeed([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10) =>
            _posts.GetFollowingFeedAsync(CurrentUserId, pageNumber, pageSize, HttpContext.RequestAborted);


        [HttpGet("user/{userId:guid}")]
        public Task<PagedResult<PostResponseDto>> ByUser(Guid userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10) =>
            _posts.GetUserPostsAsync(userId, pageNumber, pageSize, ViewerId);

        [HttpGet("{id:guid}")]
        public Task<PostResponseDto> GetById(Guid id) => _posts.GetPostAsync(id, ViewerId);

        [Authorize]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _posts.DeletePostAsync(id, CurrentUserId);
            return NoContent();
        }

        [Authorize]
        [HttpPost("{id:guid}/like")]
        public Task<LikeResultDto> Like(Guid id) => _posts.LikeAsync(id, CurrentUserId);

        [Authorize]
        [HttpDelete("{id:guid}/like")]
        public Task<LikeResultDto> Unlike(Guid id) => _posts.UnlikeAsync(id, CurrentUserId);


        [HttpGet("health")]
        public IActionResult Health() => Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}