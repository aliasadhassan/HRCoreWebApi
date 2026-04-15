namespace HR.Identity.API.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Fields for password reset functionality
        public string? PasswordResetToken { get; set; }
        public DateTime? ResetTokenExpires { get; set; }
        public ICollection<RefreshTokenConfiguration> RefreshTokens { get; set; } = new List<RefreshTokenConfiguration>();
    }
}
