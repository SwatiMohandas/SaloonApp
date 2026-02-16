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
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                var schemaPath = Path.Combine(AppContext.BaseDirectory, "DbSchema.sql");
                if (!File.Exists(schemaPath))
                {
                    // Fallback to checking source directory if running from VS/Code during dev
                    schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "DbSchema.sql");
                }

                if (File.Exists(schemaPath))
                {
                    var sql = File.ReadAllText(schemaPath);
                    using var command = new NpgsqlCommand(sql, connection);
                    command.ExecuteNonQuery();

                    // Migration for ImagePath:
                    try {
                        using var alterCmd = new NpgsqlCommand("ALTER TABLE shops ADD COLUMN IF NOT EXISTS image_path VARCHAR(255);", connection);
                        alterCmd.ExecuteNonQuery();
                    } catch { /* Ignore if it fails or already exists */ }
                    
                    _logger.LogInformation("Database execution completed successfully.");
                }
                else
                {
                    _logger.LogWarning($"Schema file not found at: {schemaPath}. Skipping initialization.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database.");
                // We might not want to throw here if we want the app to start anyway, 
                // but for MVP it's better to fail fast or just log.
            }
        }
    }
}
