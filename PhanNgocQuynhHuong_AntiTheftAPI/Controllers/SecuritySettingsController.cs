
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhanNgocQuynhHuong_AntiTheftAPI.Data;
using PhanNgocQuynhHuong_AntiTheftAPI.Dtos;
using PhanNgocQuynhHuong_AntiTheftAPI.Models;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SecuritySettingsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public SecuritySettingsController(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
    }

    // GET: api/SecuritySettings
    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var setting = await _dbContext.SecuritySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (setting == null)
        {
            setting = new SecuritySetting
            {
                UserId = userId,
                IsArmed = false,
                EnableSoundAlert = true,
                EnableEmailAlert = true,
                AlertDelaySeconds = 0,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.SecuritySettings.Add(setting);
            await _dbContext.SaveChangesAsync();
        }

        return Ok(new
        {
            setting.Id,
            setting.IsArmed,
            setting.EnableSoundAlert,
            setting.EnableEmailAlert,
            setting.AlertDelaySeconds,
            setting.UpdatedAt
        });
    }

    // PUT: api/SecuritySettings
    [HttpPut]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] SecuritySettingUpdateDto dto)
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var setting = await _dbContext.SecuritySettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (setting == null)
        {
            setting = new SecuritySetting
            {
                UserId = userId
            };

            _dbContext.SecuritySettings.Add(setting);
        }

        setting.IsArmed = dto.IsArmed;
        setting.EnableSoundAlert = dto.EnableSoundAlert;
        setting.EnableEmailAlert = dto.EnableEmailAlert;
        setting.AlertDelaySeconds = dto.AlertDelaySeconds;
        setting.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật cài đặt bảo mật thành công.",
            setting.Id,
            setting.IsArmed,
            setting.EnableSoundAlert,
            setting.EnableEmailAlert,
            setting.AlertDelaySeconds,
            setting.UpdatedAt
        });
    }
}
