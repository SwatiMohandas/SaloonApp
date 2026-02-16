using Npgsql;
using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SaloonApp.API.Data
{
    public class DatabaseInitializer
    {
        private readonly string _connectionString;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(IConfiguration configuration, ILogger<DatabaseInitializer> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _logger = logger;
        }

        public void Initialize()
        {
            Console.WriteLine("--- DATABASE INITIALIZER STARTED ---");
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();
                Console.WriteLine("--- CONNECTION OPENED ---");

                // Unconditional Force Migration
                try {
                    using var cmd = new NpgsqlCommand("ALTER TABLE shops ADD COLUMN IF NOT EXISTS open_time TIME DEFAULT '09:00:00';", connection);
                    cmd.ExecuteNonQuery();
                    Console.WriteLine("--- OPEN_TIME MIGRATION EXECUTED ---");
                } catch (Exception ex) { Console.WriteLine($"OpenTime Failure: {ex.Message}"); }

                try {
                    using var cmd = new NpgsqlCommand("ALTER TABLE shops ADD COLUMN IF NOT EXISTS close_time TIME DEFAULT '21:00:00';", connection);
                    cmd.ExecuteNonQuery();
                     Console.WriteLine("--- CLOSE_TIME MIGRATION EXECUTED ---");
                } catch (Exception ex) { Console.WriteLine($"CloseTime Failure: {ex.Message}"); }

                try {
                    using var cmd = new NpgsqlCommand("ALTER TABLE users ADD COLUMN IF NOT EXISTS mobile_number VARCHAR(15);", connection);
                    cmd.ExecuteNonQuery();
                    Console.WriteLine("--- MOBILE_NUMBER MIGRATION EXECUTED ---");
                } catch (Exception ex) { Console.WriteLine($"MobileNumber Failure: {ex.Message}"); }

                try {
                    using var cmd = new NpgsqlCommand("ALTER TABLE users ADD COLUMN IF NOT EXISTS otp_code VARCHAR(6);", connection);
                    cmd.ExecuteNonQuery();
                    Console.WriteLine("--- OTP_CODE MIGRATION EXECUTED ---");
                } catch (Exception ex) { Console.WriteLine($"OtpCode Failure: {ex.Message}"); }

                try {
                    using var cmd = new NpgsqlCommand("ALTER TABLE users ADD COLUMN IF NOT EXISTS otp_expiry TIMESTAMP;", connection);
                    cmd.ExecuteNonQuery();
                    Console.WriteLine("--- OTP_EXPIRY MIGRATION EXECUTED ---");
                } catch (Exception ex) { Console.WriteLine($"OtpExpiry Failure: {ex.Message}"); }

                try {
                    using var cmd = new NpgsqlCommand("ALTER TABLE users ADD COLUMN IF NOT EXISTS is_mobile_verified BOOLEAN DEFAULT FALSE;", connection);
                    cmd.ExecuteNonQuery();
                    Console.WriteLine("--- IS_MOBILE_VERIFIED MIGRATION EXECUTED ---");
                } catch (Exception ex) { Console.WriteLine($"IsMobileVerified Failure: {ex.Message}"); }

                try {
                    // FORCE UPDATE FUNCTION - DISABLED PER USER REQUEST
                    /*
                    using var cmd = new NpgsqlCommand(@"
                        DROP FUNCTION IF EXISTS get_nearby_shops(DECIMAL, DECIMAL, DECIMAL);
                        CREATE OR REPLACE FUNCTION get_nearby_shops(lat DECIMAL, lon DECIMAL, radius_km DECIMAL)
                        RETURNS TABLE (id INT, name VARCHAR, city VARCHAR, rating DECIMAL, distance_km DECIMAL, image_path VARCHAR, open_time TIME, close_time TIME) AS $$
                        BEGIN
                            RETURN QUERY
                            SELECT s.id, s.name, s.city, s.rating,
                            CAST((6371 * acos(cos(radians(lat)) * cos(radians(s.latitude)) * cos(radians(s.longitude) - radians(lon)) + sin(radians(lat)) * sin(radians(s.latitude)))) AS DECIMAL(10,2)) AS distance_km,
                            s.image_path,
                            s.open_time,
                            s.close_time
                            FROM shops s
                            WHERE (6371 * acos(cos(radians(lat)) * cos(radians(s.latitude)) * cos(radians(s.longitude) - radians(lon)) + sin(radians(lat)) * sin(radians(s.latitude)))) < radius_km
                            ORDER BY distance_km;
                        END;
                        $$ LANGUAGE plpgsql;
                    using var cmd = new NpgsqlCommand(@"
                        DROP FUNCTION IF EXISTS get_nearby_shops(DECIMAL, DECIMAL, DECIMAL);
                        -- ... (Function Code) ...
                    ", connection);
                    // cmd.ExecuteNonQuery(); // DISABLED
                    */

                    // Ensure Working Hours Table exists (Handled by DbSchema.sql execution below)
                    // But we might need to seed generic hours if table is empty?
                    // For now, let's just let DbSchema run.
                } catch (Exception ex) { Console.WriteLine($"Function Update Failure: {ex.Message}"); }

                var schemaPath = Path.Combine(AppContext.BaseDirectory, "DbSchema.sql");
                if (!File.Exists(schemaPath))
                {
                    schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "DbSchema.sql");
                }

                if (File.Exists(schemaPath))
                {
                    var sql = File.ReadAllText(schemaPath);
                    using var command = new NpgsqlCommand(sql, connection);
                    command.ExecuteNonQuery();
                    _logger.LogInformation("Database execution completed successfully.");
                     Console.WriteLine("--- SCHEMA SCRIPT EXECUTED ---");
                }
                else
                {
                    _logger.LogWarning($"Schema file not found at: {schemaPath}.");
                     Console.WriteLine($"--- SCHEMA FILE NOT FOUND at {schemaPath} ---");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--- FATAL ERROR: {ex} ---");
                _logger.LogError(ex, "Error initializing database.");
                throw; 
            }
        }
    }
}
