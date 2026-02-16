using Npgsql;
using SaloonApp.API.Data;
using SaloonApp.API.Models;
using System.Data;

namespace SaloonApp.API.Repositories
{
    public class QueueRepository
    {
        private readonly DatabaseContext _context;

        public QueueRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<int> JoinQueueAsync(Booking booking)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            
            command.CommandText = @"
                INSERT INTO bookings (shop_id, user_id, customer_name, status, service_id, appointment_time) 
                VALUES (@shopId, @userId, @customerName, @status, @serviceId, @appointmentTime) 
                RETURNING id";

            AddParam(command, "@shopId", booking.ShopId);
            AddParam(command, "@userId", booking.UserId == 0 ? DBNull.Value : booking.UserId);
            AddParam(command, "@customerName", booking.CustomerName);
            AddParam(command, "@status", booking.Status); 
            AddParam(command, "@serviceId", booking.ServiceId ?? (object)DBNull.Value);
            AddParam(command, "@appointmentTime", booking.AppointmentTime ?? (object)DBNull.Value);

            var result = await (command as NpgsqlCommand).ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<IEnumerable<Booking>> GetQueueByShopIdAsync(int shopId)
        {
            var results = new List<Booking>();
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            
            command.CommandText = @"
                SELECT b.*, s.name as service_name, s.duration_mins 
                FROM bookings b 
                LEFT JOIN services s ON b.service_id = s.id
                WHERE b.shop_id = @shopId 
                AND (b.status IN ('waiting', 'in_chair', 'scheduled'))
                ORDER BY b.appointment_time ASC, b.joined_at ASC";

            AddParam(command, "@shopId", shopId);

            using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var booking = MapBooking(reader);
                results.Add(booking);
            }
            return results;
        }

        public async Task<IEnumerable<Booking>> GetShopAppointmentsAsync(int shopId, DateTime date)
        {
            var results = new List<Booking>();
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();

            // Fetch 'scheduled' and 'completed' appointments for the day to block slots.
            command.CommandText = @"
                SELECT b.*, s.name as service_name, s.duration_mins 
                FROM bookings b 
                LEFT JOIN services s ON b.service_id = s.id
                WHERE b.shop_id = @shopId 
                AND b.appointment_time::date = @date::date
                AND b.status IN ('scheduled', 'waiting', 'in_chair', 'completed')
                ORDER BY b.appointment_time ASC";

            AddParam(command, "@shopId", shopId);
            AddParam(command, "@date", date);

            using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                 var booking = MapBooking(reader);
                 // Only add if it has an appointment time (ignore pure walk-ins unless we want to block them too? 
                 // Actually walk-ins usually don't have appointment_time, so this filter is implicit by the ::date query, 
                 // but let's be safe). 
                 if (booking.AppointmentTime != null) 
                    results.Add(booking);
            }
            return results;
        }

        public async Task UpdateStatusAsync(int id, string status)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            
            command.CommandText = "CALL update_queue_status(@id, @status)";
            AddParam(command, "@id", id);
            AddParam(command, "@status", status);

            await (command as NpgsqlCommand).ExecuteNonQueryAsync();
        }

        public async Task DelayBookingAsync(int id)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            
            // Move to back of line by updating joined_at to NOW
            command.CommandText = "UPDATE bookings SET joined_at = NOW() WHERE id = @id";
            AddParam(command, "@id", id);

            await (command as NpgsqlCommand).ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<Booking>> GetHistoryAsync(int userId)
        {
            var results = new List<Booking>();
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            
            command.CommandText = @"
                SELECT b.*, s.name as service_name, s.duration_mins, sh.name as shop_name, sh.city as shop_city
                FROM bookings b 
                JOIN shops sh ON b.shop_id = sh.id
                LEFT JOIN services s ON b.service_id = s.id
                WHERE b.user_id = @userId
                ORDER BY b.joined_at DESC";

            AddParam(command, "@userId", userId);

            using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var booking = MapBooking(reader);
                // Map Shop details specifically for History
                 booking.Shop = new Shop { 
                        Id = reader.GetInt32(reader.GetOrdinal("shop_id")),
                        Name = reader.GetString(reader.GetOrdinal("shop_name")),
                        City = reader.GetString(reader.GetOrdinal("shop_city"))
                };
                results.Add(booking);
            }
            return results;
        }

        private Booking MapBooking(NpgsqlDataReader reader)
        {
            var booking = new Booking
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                ShopId = reader.GetInt32(reader.GetOrdinal("shop_id")),
                UserId = reader.IsDBNull(reader.GetOrdinal("user_id")) ? 0 : reader.GetInt32(reader.GetOrdinal("user_id")),
                CustomerName = reader.IsDBNull(reader.GetOrdinal("customer_name")) ? "Guest" : reader.GetString(reader.GetOrdinal("customer_name")),
                Status = reader.GetString(reader.GetOrdinal("status")),
                JoinedAt = reader.GetDateTime(reader.GetOrdinal("joined_at")),
                AppointmentTime = reader.IsDBNull(reader.GetOrdinal("appointment_time")) ? null : reader.GetDateTime(reader.GetOrdinal("appointment_time")),
                ServiceId = reader.IsDBNull(reader.GetOrdinal("service_id")) ? null : reader.GetInt32(reader.GetOrdinal("service_id"))
            };

            // Populate service info if available
            try 
            {
                if (booking.ServiceId != null)
                {
                    booking.Service = new Service 
                    { 
                        Name = reader.GetString(reader.GetOrdinal("service_name")),
                        DurationMins = reader.GetInt32(reader.GetOrdinal("duration_mins"))
                    };
                }
            } catch {} // Ignore if service cols not present (shouldn't happen with current queries)

            return booking;
        }

        private void AddParam(IDbCommand command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }
    }
}
