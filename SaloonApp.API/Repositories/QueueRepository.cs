using Npgsql;
using SaloonApp.API.Data;
using SaloonApp.API.DTOs;
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

        //public async Task<int> JoinQueueAsync(Booking booking)
        //{
        //    using var connection = _context.CreateConnection();
        //    connection.Open();
        //    using var command = connection.CreateCommand();

        //    // For now simple insert. ADO.NET
        //    command.CommandText = @"
        //        INSERT INTO bookings (shop_id, user_id, customer_name, status, service_id) 
        //        VALUES (@shopId, @userId, @customerName, 'waiting', @serviceId) 
        //        RETURNING id";

        //    AddParam(command, "@shopId", booking.ShopId);
        //    AddParam(command, "@userId", booking.UserId == 0 ? DBNull.Value : booking.UserId);
        //    AddParam(command, "@customerName", booking.CustomerName);
        //    AddParam(command, "@serviceId", booking.ServiceId ?? (object)DBNull.Value);

        //    var result = await (command as NpgsqlCommand).ExecuteScalarAsync();
        //    return Convert.ToInt32(result);
        //}


        public async Task<(int BookingId, DateTime AppointmentTime)> JoinQueueAsync(Booking booking)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = @"
SELECT o_booking_id, o_appointment_time
FROM public.fn_join_queue(@shopId, @userId, @customerName, @serviceId, @appointmentTime);
";

            AddParam(command, "@shopId", booking.ShopId);
            AddParam(command, "@userId", booking.UserId);
            AddParam(command, "@customerName", booking.CustomerName);
            AddParam(command, "@serviceId", booking.ServiceId ?? (object)DBNull.Value);
            //AddParam(command, "@appointmentTime", booking.AppointmentTime.HasValue ? booking.AppointmentTime.Value : (object)DBNull.Value);
            var apptParam = new NpgsqlParameter("@appointmentTime", NpgsqlTypes.NpgsqlDbType.TimestampTz);
            apptParam.Value = booking.AppointmentTime.HasValue ? booking.AppointmentTime.Value.UtcDateTime : DBNull.Value;
            command.Parameters.Add(apptParam);



            Console.WriteLine(command.CommandText);
            foreach (NpgsqlParameter p in command.Parameters)
                Console.WriteLine($"{p.ParameterName} = {p.Value} ({p.NpgsqlDbType})");

            try
            {
                using var reader = await (command as NpgsqlCommand)!.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new Exception("Failed to book.");

                var bookingId = reader.GetInt32(0);
                var utc = reader.GetDateTime(1);  // appointment_time from postgres (UTC)

                // 🔴 CONVERT HERE
                var india = TimeZoneInfo.ConvertTimeFromUtc(
                    utc,
                    TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
                );

                return (reader.GetInt32(0), india);
            }
            catch (PostgresException ex) when (ex.SqlState == "P0001" || ex.SqlState == "P0002")
            {
                // Return message to controller
                throw new BusinessException(ex.MessageText);
            }
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
                WHERE b.shop_id = @shopId AND b.status IN ('waiting', 'in_chair', 'scheduled')
                ORDER BY b.joined_at ASC";

            AddParam(command, "@shopId", shopId);

            using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var booking = new Booking
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    ShopId = reader.GetInt32(reader.GetOrdinal("shop_id")),
                    // Handle nullable user_id safely
                    UserId = reader.IsDBNull(reader.GetOrdinal("user_id")) ? 0 : reader.GetInt32(reader.GetOrdinal("user_id")),
                    CustomerName = reader.IsDBNull(reader.GetOrdinal("customer_name")) ? "Guest" : reader.GetString(reader.GetOrdinal("customer_name")),
                    Status = reader.GetString(reader.GetOrdinal("status")),
                    JoinedAt = reader.GetDateTime(reader.GetOrdinal("joined_at")),
                    ServiceId = reader.IsDBNull(reader.GetOrdinal("service_id")) ? null : reader.GetInt32(reader.GetOrdinal("service_id"))
                };

                // Populate service info if available for calculating wait time
                if (booking.ServiceId != null)
                {
                    booking.Service = new Service
                    {
                        Name = reader.GetString(reader.GetOrdinal("service_name")),
                        DurationMins = reader.GetInt32(reader.GetOrdinal("duration_mins"))
                    };
                }

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
                var booking = new Booking
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    ShopId = reader.GetInt32(reader.GetOrdinal("shop_id")),
                    UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
                    CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                    Status = reader.GetString(reader.GetOrdinal("status")),
                    JoinedAt = reader.GetDateTime(reader.GetOrdinal("joined_at")),
                    ServiceId = reader.IsDBNull(reader.GetOrdinal("service_id")) ? null : reader.GetInt32(reader.GetOrdinal("service_id")),
                    Shop = new Shop
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("shop_id")),
                        Name = reader.GetString(reader.GetOrdinal("shop_name")),
                        City = reader.GetString(reader.GetOrdinal("shop_city"))
                    }
                };

                if (booking.ServiceId != null)
                {
                    booking.Service = new Service
                    {
                        Name = reader.GetString(reader.GetOrdinal("service_name")),
                        DurationMins = reader.GetInt32(reader.GetOrdinal("duration_mins"))
                    };
                }

                results.Add(booking);
            }
            return results;
        }

        public async Task<List<QueueSlotRow>> GetQueueSlotsRawAsync(int shopId, DateTime date)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = @"
                WITH wh AS (
                    SELECT w.is_closed, w.open_time, w.close_time
                    FROM shop_working_hours w
                    WHERE w.shop_id = @shopId
                      AND w.day_of_week = EXTRACT(DOW FROM @date::date)::int
                    LIMIT 1
                )
                SELECT
                    COALESCE(wh.is_closed, true) AS is_closed,
                    wh.open_time,
                    wh.close_time,
                    b.id AS booking_id,
                    b.appointment_time,
                    s.duration_mins
                FROM wh
                LEFT JOIN bookings b
                       ON b.shop_id = @shopId
                      AND b.status IN ('waiting','in_progress','scheduled')
                      AND b.appointment_time IS NOT NULL
                      -- ✅ IST day filter (Asia/Kolkata)
                      AND (b.appointment_time AT TIME ZONE 'Asia/Kolkata')::date = @date::date
                LEFT JOIN services s ON s.id = b.service_id
                ORDER BY b.appointment_time ASC, b.joined_at ASC;
            ";

            AddParam(command, "@shopId", shopId);
            AddParam(command, "@date", date.Date);

            var indiaTz = GetIndiaTz();
            var rows = new List<QueueSlotRow>();

            using var reader = await (command as Npgsql.NpgsqlCommand)!.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime? appointmentIst = null;

                if (!reader.IsDBNull(4))
                {
                    var utc = reader.GetDateTime(4); // timestamptz -> UTC DateTime
                    appointmentIst = TimeZoneInfo.ConvertTimeFromUtc(utc, indiaTz);
                }

                rows.Add(new QueueSlotRow
                {
                    IsClosed = !reader.IsDBNull(0) && reader.GetBoolean(0),
                    OpenTime = reader.IsDBNull(1) ? (TimeSpan?)null : reader.GetTimeSpan(1),
                    CloseTime = reader.IsDBNull(2) ? (TimeSpan?)null : reader.GetTimeSpan(2),
                    BookingId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                    AppointmentTime = appointmentIst, // ✅ IST for UI
                    DurationMins = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                });
            }

            return rows;
        }

        private static TimeZoneInfo GetIndiaTz()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); } // Windows
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }      // Linux
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
