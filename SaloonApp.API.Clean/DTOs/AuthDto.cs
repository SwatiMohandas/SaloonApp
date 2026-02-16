using System.ComponentModel.DataAnnotations;

namespace SaloonApp.API.DTOs
{
    public class RegisterDto
    {
        [Required]
        public string Name { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
        public string Role { get; set; }
        public string MobileNumber { get; set; }
    }

    public class VerifyOtpDto
    {
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
        public string? Otp { get; set; }
    }

    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
    }
}
