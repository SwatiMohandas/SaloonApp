using SaloonApp.API.Data;
using SaloonApp.API.Models;
using System.Data;
using Npgsql;

namespace SaloonApp.API.Repositories
{
    public class UserRepository
    {
        private readonly DatabaseContext _context;

        public UserRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM users WHERE email = @email";
            
            var param = command.CreateParameter();
            param.ParameterName = "@email";
            param.Value = email;
            command.Parameters.Add(param);

            using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Email = reader.GetString(reader.GetOrdinal("email")),
                    PasswordHash = reader.GetString(reader.GetOrdinal("password_hash")),
                    Role = reader.GetString(reader.GetOrdinal("role")),
                    MobileNumber = reader.IsDBNull(reader.GetOrdinal("mobile_number")) ? null : reader.GetString(reader.GetOrdinal("mobile_number")),
                    OtpCode = reader.IsDBNull(reader.GetOrdinal("otp_code")) ? null : reader.GetString(reader.GetOrdinal("otp_code")),
                    OtpExpiry = reader.IsDBNull(reader.GetOrdinal("otp_expiry")) ? null : reader.GetDateTime(reader.GetOrdinal("otp_expiry")),
                    IsMobileVerified = !reader.IsDBNull(reader.GetOrdinal("is_mobile_verified")) && reader.GetBoolean(reader.GetOrdinal("is_mobile_verified")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                };
            }
            return null;
        }

        public async Task<int> CreateUserAsync(User user)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO users (name, email, password_hash, role, mobile_number, otp_code, otp_expiry, is_mobile_verified) 
                VALUES (@name, @email, @hash, @role, @mobile, @otp, @expiry, @verified) 
                RETURNING id";

            AddParam(command, "@name", user.Name);
            AddParam(command, "@email", user.Email);
            AddParam(command, "@hash", user.PasswordHash);
            AddParam(command, "@role", user.Role);
            AddParam(command, "@mobile", user.MobileNumber ?? (object)DBNull.Value);
            AddParam(command, "@otp", user.OtpCode ?? (object)DBNull.Value);
            AddParam(command, "@expiry", user.OtpExpiry ?? (object)DBNull.Value);
            AddParam(command, "@verified", user.IsMobileVerified);

            var result = await (command as NpgsqlCommand).ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<User?> GetUserByMobileAsync(string mobile)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM users WHERE mobile_number = @mobile";
            
            var param = command.CreateParameter();
            param.ParameterName = "@mobile";
            param.Value = mobile;
            command.Parameters.Add(param);

            using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Email = reader.GetString(reader.GetOrdinal("email")),
                    PasswordHash = reader.GetString(reader.GetOrdinal("password_hash")),
                    Role = reader.GetString(reader.GetOrdinal("role")),
                    MobileNumber = reader.IsDBNull(reader.GetOrdinal("mobile_number")) ? null : reader.GetString(reader.GetOrdinal("mobile_number")),
                    OtpCode = reader.IsDBNull(reader.GetOrdinal("otp_code")) ? null : reader.GetString(reader.GetOrdinal("otp_code")),
                    OtpExpiry = reader.IsDBNull(reader.GetOrdinal("otp_expiry")) ? null : reader.GetDateTime(reader.GetOrdinal("otp_expiry")),
                    IsMobileVerified = !reader.IsDBNull(reader.GetOrdinal("is_mobile_verified")) && reader.GetBoolean(reader.GetOrdinal("is_mobile_verified")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                };
            }
            return null;
        }

        public async Task<bool> VerifyUserMobileAsync(string email)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE users SET is_mobile_verified = TRUE, otp_code = NULL, otp_expiry = NULL WHERE email = @email";
            AddParam(command, "@email", email);
            var rows = await (command as NpgsqlCommand).ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task UpdateOtpAsync(string email, string otp, DateTime expiry)
        {
             using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE users SET otp_code = @otp, otp_expiry = @expiry WHERE email = @email";
            AddParam(command, "@email", email);
            AddParam(command, "@otp", otp);
            AddParam(command, "@expiry", expiry);
            await (command as NpgsqlCommand).ExecuteNonQueryAsync();
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
