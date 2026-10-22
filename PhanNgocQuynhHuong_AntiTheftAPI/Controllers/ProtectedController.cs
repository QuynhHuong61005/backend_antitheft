
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProtectedController : ControllerBase
{
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        var email = User.FindFirstValue(
            JwtRegisteredClaimNames.Email);

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToList();

        return Ok(new
        {
            message = "JWT hợp lệ. Bạn đã đăng nhập.",
            userId,
            email,
            roles
        });
    }
}
