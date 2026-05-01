using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HR.Identity.API.Models
{
    public class RefreshTokenConfiguration
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; } // Foreign Key

        [Required]
        public string AccessToken { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string RefreshToken { get; set; } = string.Empty;

        public DateTime RefreshTokenCreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime RefreshTokenExpiryDate { get; set; }

        public bool IsRevoked { get; set; } = false;

        // Navigation property - EF Core khud hi link handle karega
        public virtual User User { get; set; } = null!;
    }
}
