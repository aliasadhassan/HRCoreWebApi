using Microsoft.AspNetCore.Hosting;

namespace HR.Shared.Library.Helpers
{
    public class EmailTemplatesHelper
    {
        private readonly IWebHostEnvironment _env;

        // Constructor injection se IWebHostEnvironment ko get karein
        public EmailTemplatesHelper(IWebHostEnvironment env)
        {
            _env = env;
        }
        public string GetPasswordResetEmail(string resetLink, string lifespan)
        {
            try
            {
                // WebRootPath use karein agar file wahan copy hui hai (Recommended pichle step mein)
                // Ya ContentRootPath use karein agar woh source folder mein hai
                var rootPath = _env.WebRootPath;

                // Path combine karein
                var templatePath = Path.Combine(rootPath, "EmailTemplates", "PasswordReset.html");

                // Poora HTML file read karein
                string htmlMessage = File.ReadAllText(templatePath);

                // Placeholders ko replace karein
                htmlMessage = htmlMessage.Replace("{resetLink}", resetLink);
                htmlMessage = htmlMessage.Replace("{lifespan}", lifespan);

                return htmlMessage;
            }
            catch (Exception ex)
            {
                // Error handling zaroori hai agar file na mile
                Console.WriteLine($"Error reading email template: {ex.Message}");
                // Fallback ya throw exception as needed
                return $"<p>Error loading email template. Reset link: {resetLink}</p>";
            }
        }
    }
}
