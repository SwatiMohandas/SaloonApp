namespace SaloonApp.API.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } // "customer" or "owner"
        public string Phone { get; set; }
        public string? MobileNumber { get; set; }
        public string? OtpCode { get; set; }
        public DateTime? OtpExpiry { get; set; }
        public bool IsMobileVerified { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
