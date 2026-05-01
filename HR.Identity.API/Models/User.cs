using System.ComponentModel.DataAnnotations;

namespace HR.Identity.API.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)] // Hash thora lamba hota hai isliye zyada space di hai
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Password reset functionality
        [MaxLength(100)]
        public string? PasswordResetToken { get; set; }
        public DateTime? ResetTokenExpires { get; set; }

        // Relationship
        public ICollection<RefreshTokenConfiguration> RefreshTokens { get; set; } = new List<RefreshTokenConfiguration>();
    }
}
