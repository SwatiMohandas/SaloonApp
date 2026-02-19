namespace SaloonApp.API.Models
{
    public class Shop
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal Rating { get; set; }
        public bool IsVerified { get; set; }
        public string? ImagePath { get; set; }
        public TimeSpan OpenTime { get; set; }
        public TimeSpan CloseTime { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation property for aggregation/DTOs
        public List<Service> Services { get; set; } = new();
    }

    public class Service
    {
        public int Id { get; set; }
        public int ShopId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int DurationMins { get; set; }
    }

    public class ShopSearchResult
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public decimal Rating { get; set; }
        public decimal DistanceKm { get; set; }
        public string? ImagePath { get; set; }
        public TimeOnly? OpenTime { get; set; }
        public TimeOnly? CloseTime { get; set; }

    }
    public class ReviewDto
    {
        public int Id { get; set; }
        public int ShopId { get; set; }
        public int UserId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }

        // Optional (if you join users table)
        public string? UserName { get; set; }
    }

    public class CreateReviewDto
    {
        public int ShopId { get; set; }
        public int Rating { get; set; }          // 1..5
        public string? Comment { get; set; }
    }

}
