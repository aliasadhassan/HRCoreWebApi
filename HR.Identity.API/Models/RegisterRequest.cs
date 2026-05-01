using System.ComponentModel.DataAnnotations;

namespace HR.Identity.API.Models
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Username zarori hai")]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address zarori hai")]
        [EmailAddress(ErrorMessage = "Email ka format sahi nahi hai")]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password zarori hai")]
        [MinLength(6, ErrorMessage = "Password kam az kam 6 characters ka hona chahiye")]
        public string Password { get; set; } = string.Empty;
    }
}
