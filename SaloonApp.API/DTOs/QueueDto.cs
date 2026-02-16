using System.ComponentModel.DataAnnotations;

namespace SaloonApp.API.DTOs
{
    public class JoinQueueDto
    {
        [Required]
        public int ShopId { get; set; }
        public int? ServiceId { get; set; }
        // Optional because logged-in user key can be used
        public string? CustomerName { get; set; }
        public DateTime? AppointmentTime { get; set; }
    }

    public class UpdateQueueStatusDto
    {
        [Required]
        public string Status { get; set; } // waiting, in_chair, completed, cancelled, scheduled
    }

    public class AppointmentSlotDto
    {
        public string Time { get; set; } // "14:00"
        public bool Available { get; set; }
    }
}
