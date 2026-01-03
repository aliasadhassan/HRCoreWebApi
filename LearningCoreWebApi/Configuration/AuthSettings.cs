namespace LearningCoreWebApi.Configuration
{
    public class AuthSettings
    {
        // Property name JSON key "ResetPasswordUrl" se match hona chahiye
        public string ResetPasswordUrl { get; set; } = string.Empty;

        // Agar zaroorat paray to lifespan hours ko bhi yahan add kar sakte hain
        public int ResetPasswordTokenLifespanHours { get; set; } = 24; // Default 24 hours
    }
}
