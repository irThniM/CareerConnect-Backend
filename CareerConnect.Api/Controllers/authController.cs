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
    public class authController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly PasswordHasher<User> _passwordHasher;

        public authController(AppDbContext dbContext, PasswordHasher<User> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        [HttpPost("register/candidate")]
        [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<RegisterResponse>> ResisterCandidate( RegisterRequest request)
        {
            var nomolizedEmail = request.Email.Trim().ToLower();

            var emailExists = await _dbContext.Users.AnyAsync(User =>  User.Email == nomolizedEmail);

            if (emailExists)
            {
                return BadRequest("Email đã tồn tại.");
            }

            var user = new User
            {
                Email = nomolizedEmail,
                Role = "Candidate",
                Status = "Acticve"
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _dbContext.Users.Add(user);

            await _dbContext.SaveChangesAsync();

            var response = new RegisterResponse
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role
            };

            return StatusCode(StatusCodes.Status201Created, response);
        }
    }
}
