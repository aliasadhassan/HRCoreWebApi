using System.ComponentModel.DataAnnotations;

namespace HR.Identity.API.Models
{
    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }
}
