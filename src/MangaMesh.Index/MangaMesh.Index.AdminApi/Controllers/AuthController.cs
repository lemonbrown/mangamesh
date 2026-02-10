using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Index.AdminApi.Controllers
{
    [ApiController]
    [Route("admin/auth")]
    public class AuthController : ControllerBase
    {
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // Simple hardcoded check for MVP
            if (request.Username == "admin" && request.Password == "admin")
            {
                // In a real app, return JWT or session cookie
                return Ok(new { token = "mock-jwt-token" });
            }
            return Unauthorized();
        }

        [HttpPost("verify-mfa")]
        public IActionResult VerifyMfa([FromBody] MfaRequest request)
        {
            // Mock MFA
            return Ok(new { token = "mock-jwt-token-verified" });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class MfaRequest
    {
        public string Code { get; set; } = "";
    }
}
