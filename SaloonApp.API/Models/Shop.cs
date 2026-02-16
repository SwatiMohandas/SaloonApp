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
}
