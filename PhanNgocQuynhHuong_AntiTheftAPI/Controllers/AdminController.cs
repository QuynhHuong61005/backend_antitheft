using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhanNgocQuynhHuong_AntiTheftAPI.Configurations;
using PhanNgocQuynhHuong_AntiTheftAPI.Data;
using PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Admin;
using PhanNgocQuynhHuong_AntiTheftAPI.Models;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext dbContext,
        IOptions<JwtSettings> jwtOptions,
        IWebHostEnvironment env,
        ILogger<AdminController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
        _jwtSettings = jwtOptions.Value;
        _env = env;
        _logger = logger;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
    }

    // ==========================================
    // 1. DASHBOARD
    // ==========================================

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _userManager.Users.CountAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var lockedUsersCount = await _userManager.Users
            .CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > now, cancellationToken);

        var adminRole = await _roleManager.FindByNameAsync("Admin");
        var adminCount = adminRole != null
            ? await _dbContext.UserRoles.CountAsync(ur => ur.RoleId == adminRole.Id, cancellationToken)
            : 0;

        var userRole = await _roleManager.FindByNameAsync("User");
        var userCount = userRole != null
            ? await _dbContext.UserRoles.CountAsync(ur => ur.RoleId == userRole.Id, cancellationToken)
            : 0;

        var totalAlerts = await _dbContext.AlertLogs.CountAsync(cancellationToken);

        var todayUtc = DateTime.UtcNow.Date;
        var todayAlertsCount = await _dbContext.AlertLogs
            .CountAsync(a => a.CreatedAt >= todayUtc, cancellationToken);

        var unreadAlertsCount = await _dbContext.AlertLogs
            .CountAsync(a => !a.IsRead, cancellationToken);

        var armedDevicesCount = await _dbContext.SecuritySettings
            .CountAsync(s => s.IsArmed, cancellationToken);

        var alertsByType = await _dbContext.AlertLogs
            .GroupBy(a => a.AlertType)
            .Select(g => new AlertTypeStatDto
            {
                AlertType = g.Key,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var sevenDaysAgo = todayUtc.AddDays(-6);
        var recentAlertsRaw = await _dbContext.AlertLogs
            .Where(a => a.CreatedAt >= sevenDaysAgo)
            .GroupBy(a => a.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var recentAlerts7Days = new List<DailyAlertStatDto>();
        for (int i = 6; i >= 0; i--)
        {
            var d = todayUtc.AddDays(-i);
            var match = recentAlertsRaw.FirstOrDefault(r => r.Date == d);
            recentAlerts7Days.Add(new DailyAlertStatDto
            {
                Date = d.ToString("yyyy-MM-dd"),
                Count = match?.Count ?? 0
            });
        }

        var dashboard = new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            AdminCount = adminCount,
            UserCount = userCount,
            LockedUsersCount = lockedUsersCount,
            TotalAlerts = totalAlerts,
            TodayAlertsCount = todayAlertsCount,
            UnreadAlertsCount = unreadAlertsCount,
            ArmedDevicesCount = armedDevicesCount,
            AlertsByType = alertsByType,
            RecentAlerts7Days = recentAlerts7Days,
            ServerTimeUtc = DateTime.UtcNow
        };

        return Ok(dashboard);
    }

    // ==========================================
    // 2. USER MANAGEMENT
    // ==========================================

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _userManager.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.Contains(s)) ||
                (u.Email != null && u.Email.Contains(s)) ||
                u.FullName.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var targetRole = await _roleManager.FindByNameAsync(role.Trim());
            if (targetRole != null)
            {
                var roleId = targetRole.Id;
                var userIdsInRole = _dbContext.UserRoles
                    .Where(ur => ur.RoleId == roleId)
                    .Select(ur => ur.UserId);

                query = query.Where(u => userIdsInRole.Contains(u.Id));
            }
            else
            {
                query = query.Where(u => false);
            }
        }

        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim().ToLowerInvariant();
            if (st == "locked")
            {
                query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > now);
            }
            else if (st == "active")
            {
                query = query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= now);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.FullName,
                u.EmailConfirmed,
                u.LockoutEnd,
                u.LockoutEnabled,
                u.AccessFailedCount
            })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        var userRolesList = await (from ur in _dbContext.UserRoles
                                   join r in _dbContext.Roles on ur.RoleId equals r.Id
                                   where userIds.Contains(ur.UserId)
                                   select new { ur.UserId, RoleName = r.Name })
                                  .ToListAsync(cancellationToken);

        var alertCounts = await _dbContext.AlertLogs
            .Where(a => userIds.Contains(a.UserId))
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.UserId, g => g.Count, cancellationToken);

        var armedStatuses = await _dbContext.SecuritySettings
            .Where(s => userIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, s => s.IsArmed, cancellationToken);

        var dtos = users.Select(u => new AdminUserDto
        {
            Id = u.Id,
            UserName = u.UserName,
            Email = u.Email,
            FullName = u.FullName,
            Roles = userRolesList
                .Where(r => r.UserId == u.Id && r.RoleName != null)
                .Select(r => r.RoleName!)
                .ToList(),
            EmailConfirmed = u.EmailConfirmed,
            IsLocked = u.LockoutEnd != null && u.LockoutEnd > now,
            LockoutEnd = u.LockoutEnd,
            LockoutEnabled = u.LockoutEnabled,
            AccessFailedCount = u.AccessFailedCount,
            AlertsCount = alertCounts.TryGetValue(u.Id, out var count) ? count : 0,
            IsArmed = armedStatuses.TryGetValue(u.Id, out var isArmed) ? isArmed : null
        }).ToList();

        return Ok(new PagedResult<AdminUserDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = dtos
        });
    }

    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUserById(string id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy tài khoản có ID '{id}'." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var now = DateTimeOffset.UtcNow;
        var isLocked = user.LockoutEnd != null && user.LockoutEnd > now;

        var secSetting = await _dbContext.SecuritySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == user.Id, cancellationToken);

        var totalAlerts = await _dbContext.AlertLogs
            .CountAsync(a => a.UserId == user.Id, cancellationToken);

        var recentAlerts = await _dbContext.AlertLogs
            .AsNoTracking()
            .Where(a => a.UserId == user.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => new AdminAlertItemDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = user.UserName,
                UserEmail = user.Email,
                UserFullName = user.FullName,
                AlertType = a.AlertType,
                Message = a.Message,
                ImagePath = a.ImagePath,
                IsRead = a.IsRead,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var detailDto = new AdminUserDetailDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Roles = roles,
            EmailConfirmed = user.EmailConfirmed,
            IsLocked = isLocked,
            LockoutEnd = user.LockoutEnd,
            LockoutEnabled = user.LockoutEnabled,
            AccessFailedCount = user.AccessFailedCount,
            TotalAlerts = totalAlerts,
            SecuritySetting = secSetting == null ? null : new AdminUserSecuritySettingDto
            {
                Id = secSetting.Id,
                IsArmed = secSetting.IsArmed,
                EnableSoundAlert = secSetting.EnableSoundAlert,
                EnableEmailAlert = secSetting.EnableEmailAlert,
                AlertDelaySeconds = secSetting.AlertDelaySeconds,
                UpdatedAt = secSetting.UpdatedAt
            },
            RecentAlerts = recentAlerts
        };

        return Ok(detailDto);
    }

    [HttpPut("users/{id}/lock")]
    public async Task<IActionResult> LockUser(
        string id,
        [FromBody] LockUserRequest? request,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (id == currentUserId)
        {
            return BadRequest(new { message = "Bạn không thể tự khóa tài khoản Admin của chính mình." });
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy tài khoản có ID '{id}'." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Admin"))
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var activeAdmins = adminUsers.Where(u => u.Id != id && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow));
            if (!activeAdmins.Any())
            {
                return BadRequest(new { message = "Không thể khóa tài khoản Admin đang hoạt động duy nhất của hệ thống." });
            }
        }

        user.LockoutEnabled = true;
        var lockDuration = request?.Days is > 0
            ? DateTimeOffset.UtcNow.AddDays(request.Days.Value)
            : DateTimeOffset.UtcNow.AddYears(100);

        var lockResult = await _userManager.SetLockoutEndDateAsync(user, lockDuration);
        if (!lockResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Khóa tài khoản thất bại.",
                errors = lockResult.Errors.Select(e => e.Description)
            });
        }

        await _userManager.UpdateSecurityStampAsync(user);

        _logger.LogInformation("Admin {AdminId} locked user {UserId} until {LockUntil}. Reason: {Reason}",
            currentUserId, user.Id, lockDuration, request?.Reason ?? "N/A");

        return Ok(new
        {
            success = true,
            message = "Tài khoản đã được khóa thành công.",
            userId = user.Id,
            isLocked = true,
            lockoutEnd = lockDuration,
            reason = request?.Reason
        });
    }

    [HttpPut("users/{id}/unlock")]
    public async Task<IActionResult> UnlockUser(string id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy tài khoản có ID '{id}'." });
        }

        var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!unlockResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Mở khóa tài khoản thất bại.",
                errors = unlockResult.Errors.Select(e => e.Description)
            });
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        var currentUserId = GetCurrentUserId();
        _logger.LogInformation("Admin {AdminId} unlocked user {UserId}", currentUserId, user.Id);

        return Ok(new
        {
            success = true,
            message = "Tài khoản đã được mở khóa thành công.",
            userId = user.Id,
            isLocked = false,
            lockoutEnd = (DateTimeOffset?)null
        });
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> UpdateUserRole(
        string id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestedRole = request.Role?.Trim();
        if (string.IsNullOrWhiteSpace(requestedRole) ||
            (!string.Equals(requestedRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(requestedRole, "User", StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = "Vai trò không hợp lệ. Chỉ chấp nhận 'Admin' hoặc 'User'." });
        }

        var targetRole = string.Equals(requestedRole, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "User";

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy tài khoản có ID '{id}'." });
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Contains(targetRole))
        {
            return Ok(new
            {
                success = true,
                message = $"Tài khoản đã có vai trò '{targetRole}'.",
                userId = user.Id,
                roles = currentRoles
            });
        }

        // Kiểm tra an toàn: Không cho phép hạ quyền Admin cuối cùng trong hệ thống
        if (currentRoles.Contains("Admin") && targetRole != "Admin")
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            if (adminUsers.Count <= 1)
            {
                return BadRequest(new { message = "Không thể hạ quyền Admin cuối cùng trong hệ thống." });
            }

            var currentUserId = GetCurrentUserId();
            if (user.Id == currentUserId)
            {
                return BadRequest(new { message = "Bạn không thể tự hạ quyền Admin của chính mình." });
            }
        }

        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Không thể gỡ bỏ vai trò cũ.",
                    errors = removeResult.Errors.Select(e => e.Description)
                });
            }
        }

        var addResult = await _userManager.AddToRoleAsync(user, targetRole);
        if (!addResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Không thể gán vai trò mới.",
                errors = addResult.Errors.Select(e => e.Description)
            });
        }

        await _userManager.UpdateSecurityStampAsync(user);

        var newRoles = await _userManager.GetRolesAsync(user);
        _logger.LogInformation("Admin {AdminId} changed role of user {UserId} to {NewRole}",
            GetCurrentUserId(), user.Id, targetRole);

        return Ok(new
        {
            success = true,
            message = $"Đã cập nhật vai trò tài khoản thành '{targetRole}'.",
            userId = user.Id,
            roles = newRoles
        });
    }

    [HttpGet("users/{id}/security-settings")]
    public async Task<IActionResult> GetUserSecuritySettings(
        string id,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy tài khoản có ID '{id}'." });
        }

        var setting = await _dbContext.SecuritySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == id, cancellationToken);

        if (setting == null)
        {
            return Ok(new
            {
                userId = id,
                userEmail = user.Email,
                hasSettings = false,
                message = "Người dùng chưa thiết lập cấu hình bảo mật cá nhân."
            });
        }

        return Ok(new
        {
            userId = id,
            userEmail = user.Email,
            hasSettings = true,
            setting = new AdminUserSecuritySettingDto
            {
                Id = setting.Id,
                IsArmed = setting.IsArmed,
                EnableSoundAlert = setting.EnableSoundAlert,
                EnableEmailAlert = setting.EnableEmailAlert,
                AlertDelaySeconds = setting.AlertDelaySeconds,
                UpdatedAt = setting.UpdatedAt
            }
        });
    }

    // ==========================================
    // 3. SYSTEM ALERTS MANAGEMENT
    // ==========================================

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? alertType = null,
        [FromQuery] bool? isRead = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _dbContext.AlertLogs
            .Include(a => a.User)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(a => a.UserId == userId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(alertType))
        {
            query = query.Where(a => a.AlertType == alertType.Trim());
        }

        if (isRead.HasValue)
        {
            query = query.Where(a => a.IsRead == isRead.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a => a.Message.Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AdminAlertItemDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = a.User != null ? a.User.UserName : null,
                UserEmail = a.User != null ? a.User.Email : null,
                UserFullName = a.User != null ? a.User.FullName : null,
                AlertType = a.AlertType,
                Message = a.Message,
                ImagePath = a.ImagePath,
                IsRead = a.IsRead,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<AdminAlertItemDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        });
    }

    [HttpGet("alerts/{id:int}")]
    public async Task<IActionResult> GetAlertById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var alert = await _dbContext.AlertLogs
            .Include(a => a.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (alert == null)
        {
            return NotFound(new { message = $"Không tìm thấy cảnh báo có ID {id}." });
        }

        var dto = new AdminAlertItemDto
        {
            Id = alert.Id,
            UserId = alert.UserId,
            UserName = alert.User?.UserName,
            UserEmail = alert.User?.Email,
            UserFullName = alert.User?.FullName,
            AlertType = alert.AlertType,
            Message = alert.Message,
            ImagePath = alert.ImagePath,
            IsRead = alert.IsRead,
            CreatedAt = alert.CreatedAt
        };

        return Ok(dto);
    }

    [HttpPut("alerts/{id:int}/read")]
    public async Task<IActionResult> MarkAlertAsRead(
        int id,
        CancellationToken cancellationToken = default)
    {
        var alert = await _dbContext.AlertLogs.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (alert == null)
        {
            return NotFound(new { message = $"Không tìm thấy cảnh báo có ID {id}." });
        }

        alert.IsRead = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = $"Đã đánh dấu cảnh báo #{id} là đã đọc.",
            alertId = id,
            isRead = true
        });
    }

    [HttpPut("alerts/read-all")]
    public async Task<IActionResult> MarkAllAlertsAsRead(
        CancellationToken cancellationToken = default)
    {
        var unreadAlerts = await _dbContext.AlertLogs
            .Where(a => !a.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var alert in unreadAlerts)
        {
            alert.IsRead = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = $"Đã đánh dấu {unreadAlerts.Count} cảnh báo là đã đọc.",
            updatedCount = unreadAlerts.Count
        });
    }

    // ==========================================
    // 4. ACCESS LOGS & SYSTEM INFO
    // ==========================================

    [HttpGet("access-logs")]
    public async Task<IActionResult> GetAccessLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? eventType = null,
        [FromQuery] string? userId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _dbContext.AccessLogs
            .Include(a => a.User)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(a => a.UserId == userId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(a => a.EventType == eventType.Trim());
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= toDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AdminAccessLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = a.User != null ? a.User.UserName : null,
                UserEmail = a.User != null ? a.User.Email : null,
                EventType = a.EventType,
                Description = a.Description,
                CapturedImagePath = a.CapturedImagePath,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<AdminAccessLogDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        });
    }

    [HttpGet("system-info")]
    public async Task<IActionResult> GetSystemInfo(CancellationToken cancellationToken = default)
    {
        var isDbConnected = await _dbContext.Database.CanConnectAsync(cancellationToken);
        var totalUsers = await _userManager.Users.CountAsync(cancellationToken);
        var totalAlerts = await _dbContext.AlertLogs.CountAsync(cancellationToken);
        var totalSecuritySettings = await _dbContext.SecuritySettings.CountAsync(cancellationToken);

        var info = new SystemInfoDto
        {
            Environment = _env.EnvironmentName,
            ServerTimeUtc = DateTime.UtcNow,
            JwtIssuer = _jwtSettings.Issuer,
            JwtAudience = _jwtSettings.Audience,
            EmailServiceConfigured = true,
            DatabaseConnected = isDbConnected,
            TotalUsers = totalUsers,
            TotalAlerts = totalAlerts,
            TotalSecuritySettings = totalSecuritySettings
        };

        return Ok(info);
    }
}
