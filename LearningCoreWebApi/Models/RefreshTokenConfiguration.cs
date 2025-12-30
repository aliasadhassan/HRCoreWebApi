namespace LearningCoreWebApi.Models
{
    public class RefreshTokenConfiguration
    {
        public int Id { get; set; }                 // PK
        public int UserId { get; set; }              // FK -> Users table
        public string AccessToken { get; set; } = string.Empty;
        //public string NewAccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenCreatedDate { get; set; }
        public DateTime RefreshTokenExpiryDate { get; set; }
        public bool IsRevoked { get; set; }           // token manually revoked?
        public User User { get; set; } = null!; // 🔹 Navigation property
    }
}
