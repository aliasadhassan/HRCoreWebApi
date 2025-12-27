namespace LearningCoreWebApi.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public ICollection<RefreshTokenConfiguration> RefreshTokens { get; set; } = new List<RefreshTokenConfiguration>();
    }
}
