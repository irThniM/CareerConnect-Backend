using CareerConnect.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CareerConnect.Api.Entities;
using CareerConnect.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using CareerConnect.Api.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CareerConnect.Api.Controllers
{
    [ApiController]
    [Route("api/[Controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly TokenService _tokenService;

        public AuthController(AppDbContext dbContext, 
            PasswordHasher<User> passwordHasher, 
            TokenService tokenService)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
        }

        //**********LOGIN*********
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
        {
            var normalizedEmail = request.Email.Trim().ToLower();

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail);

            if (user == null)
            {
                return Unauthorized("Email hoặc mật khẩu không đúng.");
            }

            var passwordResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized("Email hoặc mật khẩu không đúng.");
            }

            if (user.Status != "Active")
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    "Tài khoản hiện không hoạt động.");
            }

            var token = _tokenService.CreateToken(user);

            var response = new LoginResponse
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                Token = token
            };

            return Ok(response);
        }

        //**********Current User**********

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<LoginResponse>> GetCurrentUser()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Unauthorized();
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.Id == userId);

            if (user == null)
            {
                return Unauthorized();
            }

            var response = new CurrentUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
            };

            return Ok(response);
        }


        //**********REGISTER**********

        [HttpPost("register/candidate")]
        [ProducesResponseType(
            typeof(RegisterResponse),
            StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<RegisterResponse>> RegisterCandidate(RegisterRequest request)
        {
            return await RegisterUser(request, "Candidate");
        }

        [HttpPost("register/company")]
        [ProducesResponseType(
            typeof(RegisterResponse),
            StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<RegisterResponse>> RegisterCompany(RegisterRequest request)
        {
            return await RegisterUser(request, "Company");
        }

        private async Task<ActionResult<RegisterResponse>> RegisterUser(RegisterRequest request, string role)
        {
            var normalizedEmail = request.Email.Trim().ToLower();
            var emailExists = await _dbContext.Users.AnyAsync(user => user.Email == normalizedEmail);

            if (emailExists)
            {
                return Conflict("Email đã tồn tại.");
            }

            var user = new User
            {
                Email = normalizedEmail,
                Role = role,
                Status = "Active"
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var response = new RegisterResponse
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status
            };

            return StatusCode(StatusCodes.Status201Created, response);

        }

    
    }
}
