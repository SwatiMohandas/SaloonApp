using System.ComponentModel.DataAnnotations;

namespace SaloonApp.API.DTOs
{
    public class CreateReviewDto
    {
        [Required]
        public int ShopId { get; set; }
        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }
        public string Comment { get; set; }
    }
}
