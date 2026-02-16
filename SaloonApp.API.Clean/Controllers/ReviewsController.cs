using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaloonApp.API.DTOs;
using SaloonApp.API.Models;
using SaloonApp.API.Repositories;
using System.Security.Claims;

namespace SaloonApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly ReviewRepository _repository;

        public ReviewsController(ReviewRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("shop/{shopId}")]
        public async Task<IActionResult> GetShopReviews(int shopId)
        {
            var reviews = await _repository.GetReviewsByShopIdAsync(shopId);
            return Ok(reviews);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddReview([FromBody] CreateReviewDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0) return Unauthorized();

            var review = new Review
            {
                ShopId = dto.ShopId,
                UserId = userId,
                Rating = dto.Rating,
                Comment = dto.Comment
            };

            await _repository.AddReviewAsync(review);
            return Ok(new { message = "Review added successfully" });
        }
    }
}
