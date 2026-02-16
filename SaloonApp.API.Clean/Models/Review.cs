namespace SaloonApp.API.Models
{
    public class Review
    {
        public int Id { get; set; }
        public int ShopId { get; set; }
        public int UserId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation property (optional/for display)
        public string? UserName { get; set; }
    }
}
