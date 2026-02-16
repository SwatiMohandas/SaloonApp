namespace SaloonApp.API.Models
{
    using System.Text.Json.Serialization;

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
        public int ChairCount { get; set; } // Default will be handled in Repo/DB
        public DateTime CreatedAt { get; set; }
        
        // Navigation property for aggregation/DTOs
        public List<Service> Services { get; set; } = new();
        public List<ShopWorkingHour> WorkingHours { get; set; } = new();
    }

    public class ShopWorkingHour
    {
        public int Id { get; set; }
        public int ShopId { get; set; }
        public int DayOfWeek { get; set; } // 0=Sunday
        public TimeSpan? OpenTime { get; set; }
        public TimeSpan? CloseTime { get; set; }
        public bool IsClosed { get; set; }
    }

    public class Service
    {
        public int Id { get; set; }
        public int ShopId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int DurationMins { get; set; }
    }

    public class ShopSearchResultDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public decimal Rating { get; set; }
        public decimal DistanceKm { get; set; }
        public string? ImagePath { get; set; }
        
        [JsonPropertyName("openTime")]
        public string OpenTime { get; set; }
        
        [JsonPropertyName("closeTime")]
        public string CloseTime { get; set; }

        [JsonPropertyName("dailyHours")]
        public List<ShopWorkingHourDto> DailyHours { get; set; } = new();
    }

    public class ShopWorkingHourDto
    {
        [JsonPropertyName("dayOfWeek")]
        public int DayOfWeek { get; set; }

        [JsonPropertyName("openTime")]
        public string OpenTime { get; set; }

        [JsonPropertyName("closeTime")]
        public string CloseTime { get; set; }

        [JsonPropertyName("isClosed")]
        public bool IsClosed { get; set; }
    }
}
