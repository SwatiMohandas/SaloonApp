namespace SaloonApp.API.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public int ShopId { get; set; }
        public int UserId { get; set; }
        public string CustomerName { get; set; }
        public string Status { get; set; } // waiting, in_chair, completed, scheduled
        public DateTime JoinedAt { get; set; }
        public DateTimeOffset? AppointmentTime { get; set; }
        public int? ServiceId { get; set; }
        
        // Navigation properties
        public Shop Shop { get; set; }
        public User User { get; set; }
        public Service Service { get; set; }
    }
}
