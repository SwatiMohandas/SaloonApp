using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SaloonApp.API.DTOs
{
    public class CreateShopDto
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string City { get; set; }
        [Required]
        public string Address { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public IFormFile? Image { get; set; }
        public string OpenTime { get; set; } = "09:00";
        public string CloseTime { get; set; } = "21:00";
    }

    public class AddServiceDto
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public decimal Price { get; set; }
        [Required]
        public int DurationMins { get; set; }
    }
}
