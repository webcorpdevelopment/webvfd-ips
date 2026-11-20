using InvoiceScanner.Data;
using InvoiceScanner.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace InvoiceScanner.Controllers
{
    [Route("api/user")]
    [ApiController]
    [Authorize]
    public class UserApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public UserApiController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost("activity")]
        public async Task<IActionResult> UpdateActivity()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            user.LastActivity = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            return Ok(new { success = true });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyPassword([FromForm] string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return BadRequest(new { success = false, message = "Password is required" });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found" });
            }

            var isValid = await _userManager.CheckPasswordAsync(user, password);
            if (isValid)
            {
                // Update last activity
                user.LastActivity = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                return Ok(new { success = true });
            }

            return BadRequest(new { success = false, message = "Invalid password" });
        }
    }
} 