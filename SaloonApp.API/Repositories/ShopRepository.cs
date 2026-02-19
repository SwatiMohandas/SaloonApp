using Npgsql;
using NpgsqlTypes;
using SaloonApp.API.Data;
using SaloonApp.API.DTOs;
using SaloonApp.API.Models;
using System.Data;
using System.Text.Json;
using System.Transactions;

namespace SaloonApp.API.Repositories
{
    public class ShopRepository
    {
        private readonly DatabaseContext _context;

        public ShopRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ShopSearchResult>> SearchNearbyAsync(decimal lat, decimal lon, decimal radiusKm)
        {
            var results = new List<ShopSearchResult>();
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            
            // Call the Postgres function we created: get_nearby_shops
            command.CommandText = "SELECT * FROM get_nearby_shops(@lat, @lon, @radius)";
            
            AddParam(command, "@lat", lat);
            AddParam(command, "@lon", lon);
            AddParam(command, "@radius", radiusKm);

            using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new ShopSearchResult
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    City = reader.GetString(reader.GetOrdinal("city")),
                    Rating = reader.GetDecimal(reader.GetOrdinal("rating")),
                    DistanceKm = reader.GetDecimal(reader.GetOrdinal("distance_km")),
                    ImagePath = reader.IsDBNull(reader.GetOrdinal("image_path")) ? null : reader.GetString(reader.GetOrdinal("image_path")),
                    OpenTime = reader.IsDBNull(reader.GetOrdinal("open_time")) ? null : TimeOnly.FromTimeSpan(reader.GetTimeSpan(reader.GetOrdinal("open_time"))),
                    CloseTime = reader.IsDBNull(reader.GetOrdinal("close_time")) ? null : TimeOnly.FromTimeSpan(reader.GetTimeSpan(reader.GetOrdinal("close_time"))),
                });
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
                        OpenTime = reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("open_time")),
                        CloseTime = reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("close_time")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                    };
                }
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
                VALUES (@ownerId, @name, @city, @address, @lat, @lon, @imagePath, @openTime, @closeTime) 
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
                SET name = @name, city = @city, address = @address, latitude = @lat, longitude = @lon, open_time = @openTime, close_time = @closeTime
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
                    OpenTime = reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("open_time")),
                    CloseTime = reader.GetFieldValue<TimeSpan>(reader.GetOrdinal("close_time")),
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

                command.CommandText = "DELETE FROM bookings WHERE shop_id = @id";
                AddParam(command, "@id", shopId);
                await (command as NpgsqlCommand).ExecuteNonQueryAsync();

                command.Parameters.Clear();
                command.CommandText = "DELETE FROM services WHERE shop_id = @id";
                AddParam(command, "@id", shopId);
                await (command as NpgsqlCommand).ExecuteNonQueryAsync();

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

        private void AddParam(IDbCommand command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }

        public async Task UpsertShopWorkingHoursAsync(int shopId, List<ShopWorkingHourDto> hours)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                var json = System.Text.Json.JsonSerializer.Serialize(
                    hours,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    }
                );

                command.CommandText = "SELECT public.fn_shop_set_working_hours(@shop_id, @hours::jsonb)";
                AddParam(command, "@shop_id", shopId);
                AddParam(command, "@hours", json);

                await (command as Npgsql.NpgsqlCommand)!.ExecuteNonQueryAsync();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }


    }
}
