using System.ComponentModel.DataAnnotations;

namespace LearningCoreWebApi.Models
{
    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }
}
