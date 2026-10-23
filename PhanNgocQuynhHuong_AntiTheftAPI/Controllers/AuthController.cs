using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PhanNgocQuynhHuong_AntiTheftAPI.Configurations;
using PhanNgocQuynhHuong_AntiTheftAPI.DTOs;
using PhanNgocQuynhHuong_AntiTheftAPI.Models;
using PhanNgocQuynhHuong_AntiTheftAPI.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtSettings _jwtSettings;
    private readonly OtpService _otpService;

    public AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IOptions<JwtSettings> jwtOptions, 
    OtpService otpService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtSettings = jwtOptions.Value;
        _otpService = otpService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request)
    {
        var email = request.Email.Trim();

        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            return Conflict(new
            {
                message = "Email đã được sử dụng."
            });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(
            user,
            request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Không thể tạo tài khoản.",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        var roleResult = await _userManager.AddToRoleAsync(
    user,
    "User");

        if (!roleResult.Succeeded)
        {
            // Không để tài khoản chưa có role bị sử dụng như tài khoản hợp lệ.
            await _userManager.DeleteAsync(user);

            return StatusCode(500, new
            {
                message = "Không thể thiết lập quyền cho tài khoản."
            });
        }

        return StatusCode(201, new
        {
            message = "Đăng ký tài khoản thành công.",
            user = new
            {
                user.Id,
                user.FullName,
                user.Email
            }
        });
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login(
    [FromBody] LoginRequest request)
    {
        var email = request.Email.Trim();

        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Email hoặc mật khẩu không đúng."
            });
        }

        var passwordResult =
            await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: false);

        if (!passwordResult.Succeeded)
        {
            return Unauthorized(new
            {
                message = "Email hoặc mật khẩu không đúng."
            });
        }

        try
        {
            await _otpService.SendLoginOtpAsync(user.Email!);

            return Ok(new
            {
                message = "Mã OTP đã được gửi đến email của bạn.",
                requiresOtp = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine($"[AuthController] Lỗi gửi OTP: {ex}");
            return StatusCode(500, new
            {
                message = "Không gửi được OTP. Vui lòng thử lại.",
                error = ex.Message
            });
        }
    }
    [HttpPost("verify-login-otp")]
    public async Task<IActionResult> VerifyLoginOtp(
    [FromBody] VerifyLoginOtpRequest request)
    {
        var email = request.Email.Trim();

        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "OTP không hợp lệ hoặc đã hết hạn."
            });
        }

        var isValid = await _otpService.VerifyLoginOtpAsync(
            user.Email!,
            request.Otp);

        if (!isValid)
        {
            return Unauthorized(new
            {
                message = "OTP không hợp lệ hoặc đã hết hạn."
            });
        }

        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id),
        new(ClaimTypes.NameIdentifier, user.Id),
        new(JwtRegisteredClaimNames.Email, user.Email!),
        new(ClaimTypes.Name, user.UserName ?? user.Email!),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler()
            .WriteToken(token);

        return Ok(new
        {
            accessToken = tokenString,
            tokenType = "Bearer",
            expiresIn = 3600,
            user = new
            {
                id = user.Id,
                email = user.Email,
                fullName = user.FullName,
                roles
            }
        });
    }
}