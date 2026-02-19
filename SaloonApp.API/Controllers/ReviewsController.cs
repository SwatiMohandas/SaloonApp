using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SaloonApp.API.Models;
using SaloonApp.API.Repositories;
using System.Security.Claims;

namespace SaloonApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly ShopRepository _repository;

        public ReviewsController(ShopRepository repository)
        {
            _repository = repository;
        }
        [Authorize(Roles = "customer")]

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
        {
            if (dto.ShopId <= 0) return BadRequest(new { message = "ShopId is required." });
            if (dto.Rating < 1 || dto.Rating > 5) return BadRequest(new { message = "Rating must be between 1 and 5." });

            var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId) || userId <= 0)
                return Unauthorized();

            var saved = await _repository.UpsertReviewAsync(userId, dto);

            return Ok(new { message = "Review added", review = saved });
        }

        [HttpGet("shop/{shopId}")]
        public async Task<IActionResult> GetByShop(int shopId)
        {
            if (shopId <= 0) return BadRequest(new { message = "Invalid shopId" });

            var result = await _repository.GetReviewsByShopIdAsync(shopId);

            return Ok(new
            {
                shopId,
                avgRating = result.AvgRating,
                totalReviews = result.TotalCount,
                reviews = result.Reviews
            });
        }
    }
}
