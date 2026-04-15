namespace HR.Identity.API.Configuration
{
    public class EmailSettings
    {
        // Property names appsettings.json ki keys se match honi chahiye
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string From { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
