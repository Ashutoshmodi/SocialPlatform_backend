using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PostService.Models;
using PostService.Services;

namespace PostService.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly ILogger<PostsController> _logger;

        public PostsController(IPostService postService, ILogger<PostsController> logger)
        {
            _postService = postService;
            _logger = logger;
        }

        [HttpGet("feed")]
        public async Task<IActionResult> GetFeed([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var feed = await _postService.GetFeedAsync(pageNumber, pageSize);
                return Ok(feed);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching feed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPost(Guid id)
        {
            try
            {
                var post = await _postService.GetPostAsync(id);
                return Ok(post);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "Post not found" });
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }

        [Authorize]
        [HttpPost("")]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostDto dto)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
                var username = User.FindFirst("unique_name")?.Value ?? "Unknown";

                var post = await _postService.CreatePostAsync(dto, userId, username);
                return CreatedAtAction(nameof(GetPost), new { id = post.Id }, post);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { error = "Unauthorized" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating post: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
