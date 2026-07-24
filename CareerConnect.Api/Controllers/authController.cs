using CareerConnect.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CareerConnect.Api.Entities;
using CareerConnect.Api.DTOs;
using Microsoft.EntityFrameworkCore;
namespace CareerConnect.Api.Controllers
{
    [ApiController]
    [Route("api/[Controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthController(AppDbContext dbContext, PasswordHasher<User> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }


        [HttpPost("register/candidate")]
        [ProducesResponseType(
            typeof(RegisterResponse),
            StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<RegisterResponse>> RegisterCandidate(RegisterRequest request)
        {
            return await RegisterUser(request, "Candidate");
        }

        [HttpPost("register/company")]
        [ProducesResponseType(
            typeof(RegisterResponse),
            StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
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
