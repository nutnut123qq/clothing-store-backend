using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ClothingStore.API.Data;
using ClothingStore.API.Models;
using ClothingStore.API.Services;

namespace ClothingStore.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ClothingStoreContext _context;
        private readonly JwtService _jwtService;
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

        public AuthController(ClothingStoreContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
        {
            try
            {
                Console.WriteLine($"[Register] Starting registration for email: {dto.Email}");
                
                if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password))
                {
                    return BadRequest("Email and password are required");
                }

                var existing = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
                if (existing != null)
                {
                    Console.WriteLine($"[Register] Email already exists: {dto.Email}");
                    return Conflict("Email already registered");
                }

                Console.WriteLine("[Register] Creating new user...");
                var user = new User { Email = dto.Email };
                user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                Console.WriteLine($"[Register] User created with ID: {user.Id}");

                Console.WriteLine("[Register] Generating token...");
                var token = _jwtService.GenerateToken(user);
                Console.WriteLine("[Register] Token generated successfully");

                return Ok(new AuthResponseDto { Token = token, Email = user.Email });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Register ERROR] {ex.Message}");
                Console.WriteLine($"[Register ERROR] Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Registration failed", details = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        {
            try
            {
                Console.WriteLine($"[Login] Starting login for email: {dto.Email}");
                
                if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password))
                {
                    return BadRequest("Email and password are required");
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
                if (user == null)
                {
                    Console.WriteLine($"[Login] User not found: {dto.Email}");
                    return Unauthorized("Invalid credentials");
                }

                Console.WriteLine("[Login] Verifying password...");
                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
                if (result == PasswordVerificationResult.Failed)
                {
                    Console.WriteLine("[Login] Password verification failed");
                    return Unauthorized("Invalid credentials");
                }

                Console.WriteLine("[Login] Generating token...");
                var token = _jwtService.GenerateToken(user);
                Console.WriteLine("[Login] Login successful");
                
                return Ok(new AuthResponseDto { Token = token, Email = user.Email });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Login ERROR] {ex.Message}");
                Console.WriteLine($"[Login ERROR] Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Login failed", details = ex.Message });
            }
        }
    }
}
