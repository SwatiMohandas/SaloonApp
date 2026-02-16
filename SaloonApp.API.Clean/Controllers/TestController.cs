using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SaloonApp.API.Data;

namespace SaloonApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public TestController(DatabaseContext context)
        {
            _context = context;
        }

        [HttpGet("schema")]
        public async Task<IActionResult> GetSchema()
        {
            var columns = new List<string>();
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            // Query Bookings Schema
            command.CommandText = "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'bookings'";
            
            using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add($"{reader.GetString(0)} ({reader.GetString(1)})");
            }
            return Ok(columns);
        }

        [HttpGet("last-booking")]
        public async Task<IActionResult> GetLastBooking()
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, appointment_time, status FROM bookings ORDER BY id DESC LIMIT 1";
            
            using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                 var id = reader.GetInt32(0);
                 var time = reader.IsDBNull(1) ? "NULL" : reader.GetDateTime(1).ToString("O");
                 var status = reader.GetString(2);
                 return Ok($"ID: {id}, Time: {time}, Status: {status}");
            }
            return Ok("No bookings found");
        }

        [HttpGet("force-migrate")]
        public async Task<IActionResult> ForceMigrate()
        {
             using var connection = _context.CreateConnection();
            connection.Open();
            
            try {
                using var cmd1 = connection.CreateCommand();
                cmd1.CommandText = "ALTER TABLE shops ADD COLUMN IF NOT EXISTS open_time TIME DEFAULT '09:00:00';";
                await (cmd1 as NpgsqlCommand).ExecuteNonQueryAsync();
                
                using var cmd2 = connection.CreateCommand();
                cmd2.CommandText = "ALTER TABLE shops ADD COLUMN IF NOT EXISTS close_time TIME DEFAULT '21:00:00';";
                await (cmd2 as NpgsqlCommand).ExecuteNonQueryAsync();

                using var cmd3 = connection.CreateCommand();
                cmd3.CommandText = "ALTER TABLE shops ADD COLUMN IF NOT EXISTS chair_count INT DEFAULT 3;";
                await (cmd3 as NpgsqlCommand).ExecuteNonQueryAsync();
                
                return Ok("Migration attempted manually.");
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("search-debug-full")]
        public async Task<IActionResult> TestSearchFull([FromQuery] decimal lat = 11.246m, [FromQuery] decimal lon = 75.783m)
        {
            try {
                using var connection = _context.CreateConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM get_nearby_shops(@lat, @lon, @radius)";
                
                var p1 = command.CreateParameter(); p1.ParameterName = "@lat"; p1.Value = lat; command.Parameters.Add(p1);
                var p2 = command.CreateParameter(); p2.ParameterName = "@lon"; p2.Value = lon; command.Parameters.Add(p2);
                var p3 = command.CreateParameter(); p3.ParameterName = "@radius"; p3.Value = 50m; command.Parameters.Add(p3);

                using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
                var results = new List<SaloonApp.API.Models.ShopSearchResultDto>();
                while (await reader.ReadAsync())
                {
                    results.Add(new SaloonApp.API.Models.ShopSearchResultDto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        Name = reader.GetString(reader.GetOrdinal("name")),
                        City = reader.GetString(reader.GetOrdinal("city")),
                        Rating = reader.GetDecimal(reader.GetOrdinal("rating")),
                        DistanceKm = reader.GetDecimal(reader.GetOrdinal("distance_km")),
                        ImagePath = reader.IsDBNull(reader.GetOrdinal("image_path")) ? null : reader.GetString(reader.GetOrdinal("image_path")),
                        OpenTime = (reader.IsDBNull(reader.GetOrdinal("open_time")) ? new TimeSpan(9, 0, 0) : reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("open_time"))).ToString(@"hh\:mm"),
                        CloseTime = (reader.IsDBNull(reader.GetOrdinal("close_time")) ? new TimeSpan(21, 0, 0) : reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("close_time"))).ToString(@"hh\:mm")
                    });
                }
                return Ok(results);
            } catch (Exception ex) {
                return StatusCode(500, ex.ToString());
            }
        }
    }
}
