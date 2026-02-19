using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SaloonApp.API.Data;
using SaloonApp.API.DTOs;
using SaloonApp.API.Models;
using SaloonApp.API.Repositories;
using System.Security.Claims;

namespace SaloonApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QueueController : ControllerBase
    {
        private readonly QueueRepository _repository;

        public QueueController(QueueRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("{shopId:int}")]
        public async Task<IActionResult> GetShopQueue(int shopId)
        {
            var queue = await _repository.GetQueueByShopIdAsync(shopId);
            return Ok(queue);
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinQueue([FromBody] JoinQueueDto dto)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            int userId = 0;
            if (!string.IsNullOrEmpty(userIdStr)) userId = int.Parse(userIdStr);

            // Auto-fill customer name if logged in as customer
            string customerName = dto.CustomerName;
            if (string.IsNullOrEmpty(customerName) && userId > 0)
            {
                customerName = userName;
            }

            if (string.IsNullOrEmpty(customerName))
            {
                return BadRequest("Customer Name is required.");
            }

            // Logic: Owner adds walk-in OR Customer joins themselves
            // If Owner, userId might be owner's ID, but queue entry userId should be null for walk-in or specific if they have a system user. 
            // For MVP: Owner adds walk-in (UserId = null), Customer adds self (UserId = valid).

            int? finalUserId = null;
            if (role == "customer") finalUserId = userId;
            // If owner adds, it's a walk-in, so finalUserId remains null unless they can search users (advanced feature).

            var booking = new Booking
            {
                ShopId = dto.ShopId,
                UserId = finalUserId ?? 0, // 0 or null handled in Repo logic
                CustomerName = customerName,
                ServiceId = dto.ServiceId,
                AppointmentTime = dto.AppointmentTime ?? null
            };

            // Fix Repo logic to handle 0 as null if needed or just pass. 
            // Repo does: AddParam(command, "@userId", booking.UserId == 0 ? DBNull.Value : booking.UserId);
            // So 0 is fine.
            try
            {
                var result = await _repository.JoinQueueAsync(booking);
                return Ok(new { id = result.BookingId, appointmentTime = result.AppointmentTime, message = "Booked successfully" });
            }
            catch (BusinessException ex)
            {
                return Conflict(new { message = ex.Message }); // 409
            }
        }

        [Authorize(Roles = "owner")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateQueueStatusDto dto)
        {
            await _repository.UpdateStatusAsync(id, dto.Status);
            return Ok(new { message = "Status updated" });
        }

        [Authorize]
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            
            var history = await _repository.GetHistoryAsync(int.Parse(userIdStr));
            return Ok(history);
        }

        [Authorize]
        [HttpPut("{id:int}/cancel")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            // Fetch specific booking to verify ownership
            var allHistory = await _repository.GetHistoryAsync(userId);
            var booking = allHistory.FirstOrDefault(b => b.Id == id);

            if (booking == null) return NotFound("Booking not found or does not belong to you.");
            
            if (booking.Status != "waiting")
            {
                return BadRequest("Cannot cancel a booking that is already in-chair or completed.");
            }

            await _repository.UpdateStatusAsync(id, "cancelled");
            return Ok(new { message = "Booking cancelled successfully" });
        }

        // GET /api/queue/slots?shopId=11&date=2026-02-19
        [HttpGet("slots")]
        public async Task<IActionResult> GetSlots([FromQuery] int shopId, [FromQuery] DateTime date)
        {
            if (shopId <= 0) return BadRequest("shopId is required.");
            if (date == default) return BadRequest("date is required in yyyy-MM-dd format.");

            // Slot duration (minutes) for UI buttons
            const int slotStepMinutes = 15;

            var rows = await _repository.GetQueueSlotsRawAsync(shopId, date.Date);

            // If shop_working_hours missing, return empty slots
            if (rows.Count == 0)
                return Ok(new { slots = Array.Empty<object>() });

            var meta = rows[0];

            // Closed day
            if (meta.IsClosed)
                return Ok(new { slots = Array.Empty<object>() });

            var open = meta.OpenTime ?? TimeSpan.FromHours(9);
            var close = meta.CloseTime ?? TimeSpan.FromHours(21);

            // Filter actual bookings rows (those which have appointment_time)
            var bookings = rows
                .Where(r => r.AppointmentTime.HasValue && r.DurationMins.HasValue && r.DurationMins.Value > 0)
                .Select(r => new
                {
                    Start = DateTime.SpecifyKind(r.AppointmentTime!.Value, DateTimeKind.Utc).ToUniversalTime(),
                    End = DateTime.SpecifyKind(r.AppointmentTime!.Value, DateTimeKind.Utc).ToUniversalTime()
                          .AddMinutes(r.DurationMins!.Value)
                })
                .ToList();

            // Create slots
            var slots = new List<object>();
            var t = open;

            while (t < close)
            {
                var slotLocal = date.Date + t;

                // IMPORTANT: If your appointment_time is stored as local time (not UTC),
                // then DO NOT convert to UTC here. Use direct compare instead.
                // I'll do direct compare (most salon apps store local time).
                var slotStart = slotLocal;

                bool blocked = bookings.Any(b => slotStart >= b.Start && slotStart < b.End);

                slots.Add(new
                {
                    time = t.ToString(@"hh\:mm"),
                    available = !blocked
                });

                t = t.Add(TimeSpan.FromMinutes(slotStepMinutes));
            }

            return Ok(new { slots });
        }
    }
}
