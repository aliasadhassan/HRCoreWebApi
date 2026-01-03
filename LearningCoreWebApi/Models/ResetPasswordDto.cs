namespace LearningCoreWebApi.Models
{
    public class ResetPasswordDto
    {
        public required string Email { get; set; }
        public required string Token { get; set; } // Jo email se aaya
        public required string NewPassword { get; set; }
        public required string ConfirmPassword { get; set; } // Validation ke liye behtar hai
    }

}
