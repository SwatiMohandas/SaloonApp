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
    public class ShopsController : ControllerBase
    {
        private readonly ShopRepository _repository;

        public ShopsController(ShopRepository repository)
        {
            _repository = repository;
        }

        [Authorize(Roles = "owner")]
        [HttpGet("mine")]
        public async Task<IActionResult> GetMyShops()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0) return Unauthorized();

            var shops = await _repository.GetShopsByOwnerIdAsync(userId);
            return Ok(shops);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] decimal lat, [FromQuery] decimal lon, [FromQuery] decimal radius = 50)
        {
            var results = await _repository.SearchNearbyAsync(lat, lon, radius);
            return Ok(results);
        }


        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var shop = await _repository.GetShopByIdAsync(id);
            if (shop == null) return NotFound();
            return Ok(shop);
        }

        [Authorize(Roles = "owner")]
        [HttpPost]
        public async Task<IActionResult> CreateShop([FromForm] CreateShopDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0) return Unauthorized();

            string? imagePath = null;
            if (dto.Image != null && dto.Image.Length > 0)
            {
               var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
               if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
               var uniqueFileName = Guid.NewGuid().ToString() + "_" + dto.Image.FileName;
               var filePath = Path.Combine(uploadsFolder, uniqueFileName);
               using (var stream = new FileStream(filePath, FileMode.Create))
               {
                   await dto.Image.CopyToAsync(stream);
               }
               imagePath = $"/uploads/{uniqueFileName}";
            }

            var shop = new Shop
            {
                OwnerId = userId,
                Name = dto.Name,
                City = dto.City,
                Address = dto.Address,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                ImagePath = imagePath,
                OpenTime = TimeSpan.Parse(dto.OpenTime),
                CloseTime = TimeSpan.Parse(dto.CloseTime)
            };

            var id = await _repository.CreateShopAsync(shop);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }

        [Authorize(Roles = "owner")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateShop(int id, [FromForm] CreateShopDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0) return Unauthorized();

            var existingShop = await _repository.GetShopByIdAsync(id);
            if (existingShop == null) return NotFound();
            
            if (existingShop.OwnerId != userId) return Forbid();

            existingShop.Name = dto.Name;
            existingShop.City = dto.City;
            existingShop.Address = dto.Address;
            existingShop.Latitude = dto.Latitude;
            existingShop.Longitude = dto.Longitude;
            existingShop.OpenTime = TimeSpan.Parse(dto.OpenTime);
            existingShop.CloseTime = TimeSpan.Parse(dto.CloseTime);

            if (dto.Image != null && dto.Image.Length > 0)
            {
               var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
               if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
               var uniqueFileName = Guid.NewGuid().ToString() + "_" + dto.Image.FileName;
               var filePath = Path.Combine(uploadsFolder, uniqueFileName);
               using (var stream = new FileStream(filePath, FileMode.Create))
               {
                   await dto.Image.CopyToAsync(stream);
               }
               existingShop.ImagePath = $"/uploads/{uniqueFileName}";
               await _repository.UpdateShopImageAsync(id, existingShop.ImagePath); // Or rely on UpdateShopAsync if it updates all fields including image
            }

            // Ensure UpdateShopAsync saves the ImagePath too if changed?
            // Current UpdateShopAsync implementation ONLY updates explicit fields.
            // I should update UpdateShopAsync to include ImagePath OR rely on UpdateShopImageAsync separate call.
            // For safety, I'll call both or refactor UpdateShopAsync. 
            // Better: call UpdateShopAsync for fields, then UpdateShopImageAsync if new image.
            
            await _repository.UpdateShopAsync(existingShop);
            return Ok(new { message = "Shop updated" });
        }

        [Authorize(Roles = "owner")]
        [HttpPut("{id:int}/hours")]
        public async Task<IActionResult> UpdateShopHours(int id, [FromBody] List<ShopWorkingHourDto> dtos)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0) return Unauthorized();

            var shop = await _repository.GetShopByIdAsync(id);
            if (shop == null) return NotFound();
            if (shop.OwnerId != userId) return Forbid();

            var domainHours = dtos.Select(d => new ShopWorkingHour
            {
               ShopId = id,
               DayOfWeek = d.DayOfWeek,
               IsClosed = d.IsClosed,
               OpenTime = string.IsNullOrEmpty(d.OpenTime) ? null : TimeSpan.Parse(d.OpenTime),
               CloseTime = string.IsNullOrEmpty(d.CloseTime) ? null : TimeSpan.Parse(d.CloseTime)
            }).ToList();

            await _repository.UpdateShopWorkingHoursAsync(id, domainHours);
            return Ok(new { message = "Working hours updated" });
        }

        [Authorize(Roles = "owner")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteShop(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0) return Unauthorized();

            var shop = await _repository.GetShopByIdAsync(id);
            if (shop == null) return NotFound();

            if (shop.OwnerId != userId) return Forbid();

            await _repository.DeleteShopAsync(id);
            return NoContent();
        }
        [Authorize(Roles = "owner")]
        [HttpPost("{id:int}/services")]
        public async Task<IActionResult> CreateService(int id, [FromBody] CreateServiceDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0) return Unauthorized();

            var shop = await _repository.GetShopByIdAsync(id);
            if (shop == null) return NotFound();
            if (shop.OwnerId != userId) return Forbid();

            var service = new Service
            {
                ShopId = id,
                Name = dto.Name,
                Price = dto.Price,
                DurationMins = dto.DurationMins
            };

            await _repository.AddServiceAsync(service);
            return Ok(new { message = "Service added" });
        }
    }
}
