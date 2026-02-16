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
}
