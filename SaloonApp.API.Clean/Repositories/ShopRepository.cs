using Npgsql;
using SaloonApp.API.Data;
using SaloonApp.API.Models;
using System.Data;

namespace SaloonApp.API.Repositories
{
    public class ShopRepository
    {
        private readonly DatabaseContext _context;

        public ShopRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ShopSearchResultDto>> SearchNearbyAsync(decimal lat, decimal lon, decimal radiusKm)
        {
            var results = new List<ShopSearchResultDto>();
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            
            // Call the Postgres function we created: get_nearby_shops
            command.CommandText = "SELECT * FROM get_nearby_shops(@lat, @lon, @radius)";
            
            AddParam(command, "@lat", lat);
            AddParam(command, "@lon", lon);
            AddParam(command, "@radius", radiusKm);

            { // Scope for reader1
                using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new ShopSearchResultDto
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
                    Console.WriteLine($"[REPO-DEBUG] Mapped Shop: {reader.GetString(reader.GetOrdinal("name"))} | Open: {results.Last().OpenTime}");
                }
            } // reader1 disposed

            if (results.Any())
            {
                using var cmdHours = connection.CreateCommand();
                cmdHours.CommandText = "SELECT * FROM shop_working_hours WHERE shop_id = ANY(@ids)";
                var pIds = cmdHours.CreateParameter();
                pIds.ParameterName = "@ids";
                pIds.Value = results.Select(r => r.Id).ToList(); // Npgsql automatically handles List<int>
                cmdHours.Parameters.Add(pIds);

                using var r2 = await (cmdHours as NpgsqlCommand).ExecuteReaderAsync();
                while(await r2.ReadAsync())
                {
                    var sId = r2.GetInt32(r2.GetOrdinal("shop_id"));
                    var shop = results.FirstOrDefault(x => x.Id == sId);
                    if (shop != null) 
                    {
                        shop.DailyHours.Add(new ShopWorkingHourDto {
                            DayOfWeek = r2.GetInt32(r2.GetOrdinal("day_of_week")),
                            IsClosed = r2.GetBoolean(r2.GetOrdinal("is_closed")),
                            OpenTime = (r2.IsDBNull(r2.GetOrdinal("open_time")) ? new TimeSpan(9,0,0) : r2.GetFieldValue<TimeSpan>(r2.GetOrdinal("open_time"))).ToString(@"hh\:mm"),
                            CloseTime = (r2.IsDBNull(r2.GetOrdinal("close_time")) ? new TimeSpan(21,0,0) : r2.GetFieldValue<TimeSpan>(r2.GetOrdinal("close_time"))).ToString(@"hh\:mm")
                        });
                    }
                }
            }
            
            return results;
        }


        public async Task<Shop?> GetShopByIdAsync(int id)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            
            Shop? shop = null;

            // Get Shop Details
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM shops WHERE id = @id";
                AddParam(command, "@id", id);
                
                using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    shop = new Shop
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        OwnerId = reader.GetInt32(reader.GetOrdinal("owner_id")),
                        Name = reader.GetString(reader.GetOrdinal("name")),
                        City = reader.GetString(reader.GetOrdinal("city")),
                        Address = reader.GetString(reader.GetOrdinal("address")),
                        Latitude = reader.GetDecimal(reader.GetOrdinal("latitude")),
                        Longitude = reader.GetDecimal(reader.GetOrdinal("longitude")),
                        Rating = reader.GetDecimal(reader.GetOrdinal("rating")),
                        IsVerified = reader.GetBoolean(reader.GetOrdinal("is_verified")),
                        ImagePath = reader.IsDBNull(reader.GetOrdinal("image_path")) ? null : reader.GetString(reader.GetOrdinal("image_path")),
                        OpenTime = reader.IsDBNull(reader.GetOrdinal("open_time")) ? new TimeSpan(9, 0, 0) : reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("open_time")),
                        CloseTime = reader.IsDBNull(reader.GetOrdinal("close_time")) ? new TimeSpan(21, 0, 0) : reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("close_time")),
                        ChairCount = reader.IsDBNull(reader.GetOrdinal("chair_count")) ? 3 : reader.GetInt32(reader.GetOrdinal("chair_count")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                    };
                }
            }

            if (shop != null)
            {
            }
            
            if (shop != null)
            {
                // Get Services
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM services WHERE shop_id = @id";
                    AddParam(command, "@id", id);
                    
                    using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        shop.Services.Add(new Service
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("id")),
                            ShopId = reader.GetInt32(reader.GetOrdinal("shop_id")),
                            Name = reader.GetString(reader.GetOrdinal("name")),
                            Price = reader.GetDecimal(reader.GetOrdinal("price")),
                            DurationMins = reader.GetInt32(reader.GetOrdinal("duration_mins"))
                        });
                    }
                }

                // Get Working Hours
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM shop_working_hours WHERE shop_id = @id";
                    AddParam(command, "@id", id);
                    
                    using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        shop.WorkingHours.Add(new ShopWorkingHour
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("id")),
                            ShopId = reader.GetInt32(reader.GetOrdinal("shop_id")),
                            DayOfWeek = reader.GetInt32(reader.GetOrdinal("day_of_week")),
                            OpenTime = reader.IsDBNull(reader.GetOrdinal("open_time")) ? null : reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("open_time")),
                            CloseTime = reader.IsDBNull(reader.GetOrdinal("close_time")) ? null : reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("close_time")),
                            IsClosed = reader.GetBoolean(reader.GetOrdinal("is_closed"))
                        });
                    }
                }
            }

            return shop;
        }

        public async Task<int> CreateShopAsync(Shop shop)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO shops (owner_id, name, city, address, latitude, longitude, image_path, open_time, close_time) 
                VALUES (@ownerId, @name, @city, @address, @lat, @lon, @imagePath, @openTime::time, @closeTime::time) 
                RETURNING id";

            AddParam(command, "@ownerId", shop.OwnerId);
            AddParam(command, "@name", shop.Name);
            AddParam(command, "@city", shop.City);
            AddParam(command, "@address", shop.Address);
            AddParam(command, "@lat", shop.Latitude);
            AddParam(command, "@lon", shop.Longitude);
            AddParam(command, "@imagePath", shop.ImagePath ?? (object)DBNull.Value);
            AddParam(command, "@openTime", shop.OpenTime);
            AddParam(command, "@closeTime", shop.CloseTime);

            var result = await (command as NpgsqlCommand).ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task UpdateShopImageAsync(int shopId, string imagePath)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE shops SET image_path = @imagePath WHERE id = @id";
            AddParam(command, "@id", shopId);
            AddParam(command, "@imagePath", imagePath);
            await (command as NpgsqlCommand).ExecuteNonQueryAsync();
        }

        public async Task UpdateShopAsync(Shop shop)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE shops 
                SET name = @name, city = @city, address = @address, latitude = @lat, longitude = @lon, open_time = @openTime::time, close_time = @closeTime::time
                WHERE id = @id";

            AddParam(command, "@id", shop.Id);
            AddParam(command, "@name", shop.Name);
            AddParam(command, "@city", shop.City);
            AddParam(command, "@address", shop.Address);
            AddParam(command, "@lat", shop.Latitude);
            AddParam(command, "@lon", shop.Longitude);
            AddParam(command, "@openTime", shop.OpenTime);
            AddParam(command, "@closeTime", shop.CloseTime);

            await (command as NpgsqlCommand).ExecuteNonQueryAsync();
        }

        public async Task AddServiceAsync(Service service)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO services (shop_id, name, price, duration_mins) 
                VALUES (@shopId, @name, @price, @duration) 
                RETURNING id";

            AddParam(command, "@shopId", service.ShopId);
            AddParam(command, "@name", service.Name);
            AddParam(command, "@price", service.Price);
            AddParam(command, "@duration", service.DurationMins);

            await (command as NpgsqlCommand).ExecuteScalarAsync();
        }

        public async Task<IEnumerable<Shop>> GetShopsByOwnerIdAsync(int ownerId)
        {
            var shops = new List<Shop>();
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM shops WHERE owner_id = @ownerId";
            AddParam(command, "@ownerId", ownerId);

            using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                shops.Add(new Shop
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    OwnerId = reader.GetInt32(reader.GetOrdinal("owner_id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    City = reader.GetString(reader.GetOrdinal("city")),
                    Address = reader.GetString(reader.GetOrdinal("address")),
                    Latitude = reader.GetDecimal(reader.GetOrdinal("latitude")),
                    Longitude = reader.GetDecimal(reader.GetOrdinal("longitude")),
                    Rating = reader.GetDecimal(reader.GetOrdinal("rating")),
                    IsVerified = reader.GetBoolean(reader.GetOrdinal("is_verified")),
                    ImagePath = reader.IsDBNull(reader.GetOrdinal("image_path")) ? null : reader.GetString(reader.GetOrdinal("image_path")),
                    OpenTime = reader.IsDBNull(reader.GetOrdinal("open_time")) ? new TimeSpan(9, 0, 0) : reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("open_time")),
                    CloseTime = reader.IsDBNull(reader.GetOrdinal("close_time")) ? new TimeSpan(21, 0, 0) : reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("close_time")),
                    ChairCount = reader.IsDBNull(reader.GetOrdinal("chair_count")) ? 3 : reader.GetInt32(reader.GetOrdinal("chair_count")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                });
            }
            return shops;
        }

        public async Task DeleteShopAsync(int shopId)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                // 1. Delete Bookings (depend on shop)
                // Note: If bookings reference services, we might need to delete bookings first because services reference shop?
                // Schema: bookings -> shops(id), bookings -> services(id)
                // Services -> shops(id)
                // So deleting bookings first removes references to both services and shops.
                
                command.CommandText = "DELETE FROM bookings WHERE shop_id = @id";
                AddParam(command, "@id", shopId);
                await (command as NpgsqlCommand).ExecuteNonQueryAsync();

                // 2. Delete Services (depend on shop)
                command.Parameters.Clear();
                command.CommandText = "DELETE FROM services WHERE shop_id = @id";
                AddParam(command, "@id", shopId);
                await (command as NpgsqlCommand).ExecuteNonQueryAsync();

                // 3. Delete Shop
                command.Parameters.Clear();
                command.CommandText = "DELETE FROM shops WHERE id = @id";
                AddParam(command, "@id", shopId);
                await (command as NpgsqlCommand).ExecuteNonQueryAsync();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task UpdateShopWorkingHoursAsync(int shopId, List<ShopWorkingHour> hours)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                // 1. Delete existing hours for this shop (Replace strategy)
                command.CommandText = "DELETE FROM shop_working_hours WHERE shop_id = @id";
                AddParam(command, "@id", shopId);
                await (command as NpgsqlCommand).ExecuteNonQueryAsync();

                // 2. Insert new hours
                foreach (var h in hours)
                {
                    command.Parameters.Clear();
                    command.CommandText = @"
                        INSERT INTO shop_working_hours (shop_id, day_of_week, open_time, close_time, is_closed)
                        VALUES (@shopId, @dayOfWeek, @openTime, @closeTime, @isClosed)";
                    
                    AddParam(command, "@shopId", shopId);
                    AddParam(command, "@dayOfWeek", h.DayOfWeek);
                    AddParam(command, "@openTime", h.OpenTime ?? (object)DBNull.Value);
                    AddParam(command, "@closeTime", h.CloseTime ?? (object)DBNull.Value);
                    AddParam(command, "@isClosed", h.IsClosed);

                    await (command as NpgsqlCommand).ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
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
