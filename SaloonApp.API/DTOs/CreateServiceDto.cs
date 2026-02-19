using System.ComponentModel.DataAnnotations;

namespace SaloonApp.API.DTOs
{
    public class CreateServiceDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0, 100000)]
        public decimal Price { get; set; }

        [Required]
        [Range(1, 1440)]
        public int DurationMins { get; set; }
    }
    public class ShopWorkingHourDto
    {
        public int DayOfWeek { get; set; }          // 0..6
        public string OpenTime { get; set; } = "";  // "09:00" or "09:00:00"
        public string CloseTime { get; set; } = ""; // "21:00" or "21:00:00"
        public bool IsClosed { get; set; }
    }

}
