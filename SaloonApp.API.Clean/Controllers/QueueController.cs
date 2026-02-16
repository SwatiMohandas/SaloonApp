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
    public class QueueController : ControllerBase
    {
        private readonly QueueRepository _repository;
        private readonly ShopRepository _shopRepository; // Inject ShopRepo

        public QueueController(QueueRepository repository, ShopRepository shopRepository)
        {
            _repository = repository;
            _shopRepository = shopRepository;
        }

        [HttpGet("{shopId:int}")]
        public async Task<IActionResult> GetShopQueue(int shopId)
        {
            var queue = await _repository.GetQueueByShopIdAsync(shopId);
            // Explicit mapping to ensure JSON compatibility and correct casing
            var mappedQueue = queue.Select(b => new {
                id = b.Id,
                customerName = b.CustomerName,
                status = b.Status,
                appointmentTime = b.AppointmentTime,
                joinedAt = b.JoinedAt,
                service = b.Service == null ? null : new {
                    name = b.Service.Name,
                    durationMins = b.Service.DurationMins
                }
            });
            return Ok(mappedQueue);
        }

        [HttpGet("slots")]
        public async Task<IActionResult> GetSlots([FromQuery] int shopId, [FromQuery] DateTime date)
        {
            var shop = await _shopRepository.GetShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop not found");

            var appointments = await _repository.GetShopAppointmentsAsync(shopId, date);
            var slots = new List<AppointmentSlotDto>();

            // Default logic: 30 min slots
            TimeSpan slotDuration = TimeSpan.FromMinutes(30);
            TimeSpan start = shop.OpenTime;
            TimeSpan end = shop.CloseTime;

            // Check for specific working hours
            int dayOfWeek = (int)date.DayOfWeek;
            var dayHours = shop.WorkingHours.FirstOrDefault(h => h.DayOfWeek == dayOfWeek);
            
            if (dayHours != null)
            {
                // If closed, return empty
                if (dayHours.IsClosed)
                {
                     Console.WriteLine($"[DEBUG-SLOTS] Shop is Closed on {date.DayOfWeek}");
                     return Ok(new List<AppointmentSlotDto>());
                }

                // If specific hours set, use them
                if (dayHours.OpenTime.HasValue && dayHours.CloseTime.HasValue)
                {
                    start = dayHours.OpenTime.Value;
                    end = dayHours.CloseTime.Value;
                }
            }

            // Current time check if date is today
            var now = DateTime.Now;
            bool isToday = date.Date == now.Date;

            Console.WriteLine($"[DEBUG-SLOTS] Checking Slots for Shop {shopId} on {date.ToShortDateString()}");
            foreach(var app in appointments) {
                 if(app.AppointmentTime.HasValue)
                     Console.WriteLine($"[DEBUG-SLOTS] Found Booking: {app.AppointmentTime} (Kind: {app.AppointmentTime.Value.Kind})");
            }

            while (start.Add(slotDuration) <= end)
            {
                // Check if slot is in the past (for today)
                if (isToday && start < now.TimeOfDay)
                {
                    start = start.Add(slotDuration);
                    continue;
                }

                // Check concurrent usage against ChairCount
                int concurrentBookings = appointments.Count(a => 
                    a.AppointmentTime.HasValue && 
                    (a.AppointmentTime.Value.ToLocalTime().TimeOfDay == start || a.AppointmentTime.Value.TimeOfDay == start)
                );

                slots.Add(new AppointmentSlotDto 
                { 
                    Time = start.ToString(@"hh\:mm"), 
                    Available = concurrentBookings < shop.ChairCount 
                });

                start = start.Add(slotDuration);
            }
            
            return Ok(slots);
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinQueue([FromBody] JoinQueueDto dto)
        {
            Console.WriteLine($"[QUEUE LOG] Join Request: Shop={dto.ShopId}, Service={dto.ServiceId}, Appt={dto.AppointmentTime}");

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;

            int userId = 0;
            if (!string.IsNullOrEmpty(userIdStr)) userId = int.Parse(userIdStr);

            string customerName = dto.CustomerName;
            if (string.IsNullOrEmpty(customerName) && userId > 0)
            {
                customerName = userName;
            }

            if (string.IsNullOrEmpty(customerName))
            {
                return BadRequest("Customer Name is required.");
            }

            int? finalUserId = null;
            if (role == "customer") finalUserId = userId;

            string status = "waiting";
            
            // 1. Fetch Shop details to check hours and Capacity
            var shop = await _shopRepository.GetShopByIdAsync(dto.ShopId);
            if (shop == null) return NotFound("Shop not found");

            var now = DateTime.Now;
            bool isOpen = IsShopOpen(shop, now);
            
            Console.WriteLine($"[QUEUE-DEBUG] Shop {shop.Id} Status at {now}: Open? {isOpen}");

            // 2. Handle Appointment vs Walk-in
            if (dto.AppointmentTime.HasValue)
            {
                status = "scheduled";
                
                // Capacity Check
                var appointments = await _repository.GetShopAppointmentsAsync(dto.ShopId, dto.AppointmentTime.Value);
                int concurrentBookings = appointments.Count(a => 
                    a.AppointmentTime.HasValue && 
                    (a.AppointmentTime.Value.ToLocalTime().TimeOfDay == dto.AppointmentTime.Value.ToLocalTime().TimeOfDay)
                );
                
                if (concurrentBookings >= shop.ChairCount) {
                    return Conflict($"No available chairs at {dto.AppointmentTime.Value.ToShortTimeString()}. Capacity: {shop.ChairCount}");
                }
            }
            else 
            {
                // Walk-in / Join Now
                if (!isOpen)
                {
                    // Find Next Opening Time
                    DateTime? nextOpen = GetNextOpeningTime(shop, now);

                    if (nextOpen.HasValue) {
                        // Check Capacity for this auto-assigned slot
                        // Fetch ALL appointments for that day ONCE
                        var appointments = await _repository.GetShopAppointmentsAsync(dto.ShopId, nextOpen.Value);
                        
                        // Find first slot where capacity is available
                        DateTime candidate = nextOpen.Value;
                        bool found = false;
                        
                        // Limit search to the Shop's Closing Time for that day (or default 9PM)
                        // retrieving correct close time for that specific day
                        TimeSpan closeTime = shop.CloseTime;
                        var dayH = shop.WorkingHours.FirstOrDefault(h => h.DayOfWeek == (int) candidate.DayOfWeek);
                        if (dayH != null && dayH.CloseTime.HasValue) closeTime = dayH.CloseTime.Value;

                        while (candidate.TimeOfDay < closeTime)
                        {
                             int concurrentBookings = appointments.Count(a => 
                                a.AppointmentTime.HasValue && 
                                (a.AppointmentTime.Value.TimeOfDay == candidate.TimeOfDay)
                            );

                            if (concurrentBookings < shop.ChairCount) 
                            {
                                dto.AppointmentTime = candidate;
                                status = "scheduled";
                                found = true;
                                Console.WriteLine($"[QUEUE-LOG] Found Slot for Walk-in: {candidate}");
                                break;
                            }
                            
                            candidate = candidate.AddMinutes(30); // Try next 30 min slot
                        }

                        if (!found) {
                             return BadRequest($"Shop is closed and fully booked for {nextOpen.Value.ToShortDateString()}. Please choose a different date via Schedule.");
                        }

                    } else {
                         return BadRequest("Shop is closed and no opening hours found for the next 7 days.");
                    }
                }
            }


            var booking = new Booking
            {
                ShopId = dto.ShopId,
                UserId = finalUserId ?? 0, 
                CustomerName = customerName,
                ServiceId = dto.ServiceId,
                AppointmentTime = dto.AppointmentTime,
                Status = status
            };

            var id = await _repository.JoinQueueAsync(booking);
            return Ok(new { id, message = dto.AppointmentTime.HasValue ? "Appointment booked successfully" : "Joined queue successfully" });
        }

        private bool IsShopOpen(Shop shop, DateTime time)
        {
            TimeSpan currentOpen = shop.OpenTime;
            TimeSpan currentClose = shop.CloseTime;
            var dow = (int)time.DayOfWeek;
            var dayHours = shop.WorkingHours.FirstOrDefault(h => h.DayOfWeek == dow);
            
            Console.WriteLine($"[OPEN-CHECK] DOW: {dow}, FoundHours: {dayHours != null}");
            if (dayHours != null) {
                Console.WriteLine($"[OPEN-CHECK] DayHours: Closed? {dayHours.IsClosed}, Open: {dayHours.OpenTime}, Close: {dayHours.CloseTime}");
                if (!dayHours.IsClosed && dayHours.OpenTime.HasValue && dayHours.CloseTime.HasValue) {
                    currentOpen = dayHours.OpenTime.Value;
                    currentClose = dayHours.CloseTime.Value;
                    if (time.TimeOfDay >= currentOpen && time.TimeOfDay < currentClose) return true;
                }
            } else {
                 Console.WriteLine($"[OPEN-CHECK] Default Hours: Open: {currentOpen}, Close: {currentClose}");
                 if (time.TimeOfDay >= currentOpen && time.TimeOfDay < currentClose) return true;
            }
            return false;
        }

        private DateTime? GetNextOpeningTime(Shop shop, DateTime fromTime)
        {
            DateTime now = fromTime;
            
            // Check Today Later
            var todayDow = (int)now.DayOfWeek;
            var todayHours = shop.WorkingHours.FirstOrDefault(h => h.DayOfWeek == todayDow);
            
            if (todayHours != null) {
                 if (!todayHours.IsClosed && todayHours.OpenTime.HasValue && now.TimeOfDay < todayHours.OpenTime.Value) {
                     return now.Date + todayHours.OpenTime.Value;
                 }
            } else {
                 if (now.TimeOfDay < shop.OpenTime) {
                     return now.Date + shop.OpenTime;
                 }
            }

            // Loop Next 7 Days
            for(int i=1; i<=7; i++) {
                var d = now.AddDays(i);
                var dow = (int)d.DayOfWeek;
                var h = shop.WorkingHours.FirstOrDefault(x => x.DayOfWeek == dow);
                
                if (h != null) {
                    if (!h.IsClosed && h.OpenTime.HasValue) {
                         return d.Date + h.OpenTime.Value;
                    }
                } else {
                    // Default hours
                    return d.Date + shop.OpenTime;
                }
            }
            return null;
        }

        [Authorize(Roles = "owner")]
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateQueueStatusDto dto)
        {
            await _repository.UpdateStatusAsync(id, dto.Status);
            return Ok(new { message = "Status updated" });
        }

        [Authorize(Roles = "owner")]
        [HttpPut("{id:int}/delay")]
        public async Task<IActionResult> Delay(int id)
        {
            await _repository.DelayBookingAsync(id);
            return Ok(new { message = "Booking pushed to back of queue" });
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

            var allHistory = await _repository.GetHistoryAsync(userId);
            var booking = allHistory.FirstOrDefault(b => b.Id == id);

            if (booking == null) return NotFound("Booking not found or does not belong to you.");
            
            if (booking.Status != "waiting" && booking.Status != "scheduled")
            {
                return BadRequest("Cannot cancel a booking that is already in-chair or completed.");
            }

            await _repository.UpdateStatusAsync(id, "cancelled");
            return Ok(new { message = "Booking cancelled successfully" });
        }
    }
}
