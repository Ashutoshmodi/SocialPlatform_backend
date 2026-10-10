using System.ComponentModel.DataAnnotations;

namespace PostService.Models
{
    public class CreatePostDto
    {
        [Required(ErrorMessage = "Post can't be empty")]
        [MaxLength(500, ErrorMessage = "Post can be at most 500 characters")]
        public string Content { get; set; } = "";
    }
}
