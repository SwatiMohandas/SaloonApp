using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SaloonApp.API.DTOs;
using SaloonApp.API.Models;
using SaloonApp.API.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SaloonApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserRepository _repository;
        private readonly IConfiguration _configuration;

        public AuthController(UserRepository repository, IConfiguration configuration)
        {
            _repository = repository;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            dto.Email = dto.Email?.ToLower().Trim();
            dto.Role = dto.Role?.ToLower().Trim();

            if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Role)) 
                 return BadRequest("Email and Role are required.");

            if (await _repository.GetUserByEmailAsync(dto.Email) != null)
                return BadRequest("Email already exists");

            if (dto.Role != "customer" && dto.Role != "owner")
                return BadRequest("Invalid role. Must be 'customer' or 'owner'.");

            // Generate OTP
            var otp = new Random().Next(100000, 999999).ToString();
            var otpExpiry = DateTime.UtcNow.AddMinutes(10); // 10 min expiry

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Role = dto.Role,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                MobileNumber = dto.MobileNumber,
                OtpCode = otp,
                OtpExpiry = otpExpiry,
                IsMobileVerified = false
            };

            await _repository.CreateUserAsync(user);
            
            // In real app, send SMS here. For now, log it.
            Console.WriteLine($"[OTP SIMULATION] OTP for {dto.Email} ({dto.MobileNumber}): {otp}");

            // DEV ONLY: Return OTP in response for testing
            return Ok(new { message = "Registration successful. Please verify OTP.", email = dto.Email, requiresOtp = true, devOtp = otp });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            Console.WriteLine($"[VerifyOtp] Request: Email='{dto.Email}', Mobile='{dto.MobileNumber}', OTP='{dto.Otp}'");

            var user = await _repository.GetUserByEmailAsync(dto.Email?.ToLower().Trim());
            if (user == null) 
            {
                Console.WriteLine("[VerifyOtp] User not found by email.");
                return BadRequest("User not found.");
            }

            Console.WriteLine($"[VerifyOtp] Found User: ID={user.Id}, StoredOTP='{user.OtpCode}'");

            if (user.IsMobileVerified) return Ok(new { message = "Already verified." });

            if (user.OtpCode != dto.Otp?.Trim()) 
            {
                Console.WriteLine($"[VerifyOtp] OTP Mismatch! Expected: '{user.OtpCode}', Received: '{dto.Otp}'");
                return BadRequest("Invalid OTP.");
            }

            if (user.OtpExpiry < DateTime.UtcNow) return BadRequest("OTP Expired.");

            await _repository.VerifyUserMobileAsync(dto.Email);
            
            return Ok(new { message = "Mobile verified successfully. You can now login." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            dto.Email = dto.Email.ToLower().Trim(); 
            var user = await _repository.GetUserByEmailAsync(dto.Email);
            
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                 return Unauthorized("Invalid credentials");
            }

            if (!user.IsMobileVerified)
            {
                // Generate new OTP
                var otp = new Random().Next(100000, 999999).ToString();
                var otpExpiry = DateTime.UtcNow.AddMinutes(10);
                
                await _repository.UpdateOtpAsync(user.Email, otp, otpExpiry);
                
                // Log for simulation
                Console.WriteLine($"[OTP SIMULATION] Login OTP for {dto.Email} ({user.MobileNumber}): {otp}");

                return StatusCode(403, new { message = "Mobile not verified.", requiresOtp = true, email = user.Email, mobileNumber = user.MobileNumber, devOtp = otp });
            }

            var token = GenerateJwtToken(user);
            return Ok(new { token, user.Role, user.Name });
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("role", user.Role),
                new Claim("name", user.Name)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
