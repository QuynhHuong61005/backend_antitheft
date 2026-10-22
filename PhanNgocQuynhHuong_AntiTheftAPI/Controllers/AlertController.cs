using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhanNgocQuynhHuong_AntiTheftAPI.Data;
using PhanNgocQuynhHuong_AntiTheftAPI.DTOs;
using PhanNgocQuynhHuong_AntiTheftAPI.Models;
using PhanNgocQuynhHuong_AntiTheftAPI.Services;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AlertController : ControllerBase
    {
        private const int SoundCooldownSeconds = 30;
        private readonly EmailService _emailService;
        private readonly ApplicationDbContext _dbContext;

        public AlertController(
            EmailService emailService,
            ApplicationDbContext dbContext)
        {
            _emailService = emailService;
            _dbContext = dbContext;
        }

        [HttpPost("motion")]
        public async Task<IActionResult> SendMotionAlert()
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            var email =
                User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("email");

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "JWT không chứa ID người dùng."
                });
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "JWT không chứa email người dùng."
                });
            }

            const string subject = "AntiTheft - Cảnh báo chuyển động";

            const string message =
                "AntiTheft đã phát hiện chuyển động bất thường " +
                "khi chế độ bảo vệ được bật.";

            // Lưu lịch sử cảnh báo trước khi gửi email.
            var alertLog = new AlertLog
            {
                UserId = userId,
                AlertType = "Motion",
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _dbContext.AlertLogs.Add(alertLog);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Không thể lưu lịch sử cảnh báo."
                });
            }

            var body = $"""
                Hệ thống AntiTheft đã phát hiện chuyển động bất thường
                khi chế độ bảo vệ được bật.

                Thời gian ghi nhận (UTC): {alertLog.CreatedAt:yyyy-MM-dd HH:mm:ss}

                Nếu bạn không thực hiện hành động này, hãy kiểm tra thiết bị.
                """;

            try
            {
                await _emailService.SendEmailAsync(
                    email,
                    subject,
                    body);

                return Ok(new
                {
                    success = true,
                    message = "Đã lưu lịch sử và gửi email cảnh báo.",
                    alertLogId = alertLog.Id,
                    createdAt = alertLog.CreatedAt
                });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message =
                        "Đã lưu lịch sử cảnh báo nhưng gửi email thất bại.",
                    alertLogId = alertLog.Id
                });
            }
        }

        [HttpPost("sound")]
        public async Task<IActionResult> SendSoundAlert([FromBody] SoundAlertRequest request)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "JWT không chứa ID người dùng."
                });
            }

            var email =
                User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("email");

            if (string.IsNullOrWhiteSpace(email))
            {
                var user = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                email = user?.Email;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "JWT không chứa email người dùng và không tìm thấy email tài khoản."
                });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu âm thanh không hợp lệ.",
                    errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                });
            }

            if (request.Threshold.HasValue && request.SoundLevel < request.Threshold.Value)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Mức âm thanh ({request.SoundLevel:F1} dB) chưa vượt ngưỡng quy định ({request.Threshold.Value:F1} dB)."
                });
            }

            if (request.DetectedAt.HasValue &&
                request.DetectedAt.Value.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Thời điểm phát hiện không thể vượt quá thời gian hiện tại."
                });
            }

            // Chống spam / Cooldown 30s giữa các lần cảnh báo âm thanh của cùng một User
            var lastSoundAlert = await _dbContext.AlertLogs
                .AsNoTracking()
                .Where(a => a.UserId == userId && a.AlertType == "Sound")
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastSoundAlert != null)
            {
                var timeSinceLast = DateTime.UtcNow - lastSoundAlert.CreatedAt;
                if (timeSinceLast < TimeSpan.FromSeconds(SoundCooldownSeconds))
                {
                    var remaining = (int)Math.Ceiling(SoundCooldownSeconds - timeSinceLast.TotalSeconds);
                    return StatusCode(StatusCodes.Status429TooManyRequests, new
                    {
                        success = false,
                        message = $"Cảnh báo âm thanh đang trong thời gian giãn cách ({remaining} giây còn lại). Vui lòng chờ.",
                        retryAfterSeconds = remaining
                    });
                }
            }

            // Kiểm tra cấu hình bảo mật của User
            var setting = await _dbContext.SecuritySettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            var detectedTime = request.DetectedAt?.ToUniversalTime() ?? DateTime.UtcNow;

            var message = request.Threshold.HasValue
                ? $"AntiTheft đã phát hiện âm thanh bất thường: {request.SoundLevel:F1} dB (ngưỡng: {request.Threshold.Value:F1} dB)."
                : $"AntiTheft đã phát hiện âm thanh bất thường: {request.SoundLevel:F1} dB.";

            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                message += $" Ghi chú: {request.Description.Trim()}";
                if (message.Length > 500)
                {
                    message = message.Substring(0, 500);
                }
            }

            var alertLog = new AlertLog
            {
                UserId = userId,
                AlertType = "Sound",
                Message = message,
                IsRead = false,
                CreatedAt = detectedTime
            };

            try
            {
                _dbContext.AlertLogs.Add(alertLog);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Không thể lưu lịch sử cảnh báo."
                });
            }

            // Kiểm tra nghiệp vụ gửi email theo SecuritySetting
            var shouldSendEmail = setting == null || setting.EnableEmailAlert;

            if (!shouldSendEmail)
            {
                return Ok(new
                {
                    success = true,
                    message = "Đã lưu lịch sử cảnh báo âm thanh (email cảnh báo đang tắt trong cài đặt).",
                    alertLogId = alertLog.Id,
                    createdAt = alertLog.CreatedAt
                });
            }

            const string subject = "AntiTheft - Cảnh báo âm thanh bất thường";

            var body = $"""
                Hệ thống AntiTheft đã phát hiện âm thanh bất thường vượt ngưỡng cho phép.

                Mức âm thanh: {request.SoundLevel:F1} dB
                Ngưỡng cảnh báo: {(request.Threshold.HasValue ? $"{request.Threshold.Value:F1} dB" : "Mặc định")}
                Thời gian ghi nhận (UTC): {alertLog.CreatedAt:yyyy-MM-dd HH:mm:ss}
                {(string.IsNullOrWhiteSpace(request.Description) ? "" : $"Ghi chú: {request.Description.Trim()}\n")}
                Nếu bạn không thực hiện hành động này, hãy kiểm tra thiết bị ngay lập tức.
                """;

            try
            {
                await _emailService.SendEmailAsync(
                    email,
                    subject,
                    body);

                return Ok(new
                {
                    success = true,
                    message = "Đã lưu lịch sử và gửi email cảnh báo âm thanh.",
                    alertLogId = alertLog.Id,
                    createdAt = alertLog.CreatedAt
                });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Đã lưu lịch sử cảnh báo nhưng gửi email thất bại.",
                    alertLogId = alertLog.Id
                });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? alertType = null)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "JWT không chứa ID người dùng."
                });
            }

            if (page < 1 || pageSize < 1 || pageSize > 50)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "page phải >= 1 và pageSize phải từ 1 đến 50."
                });
            }

            var query = _dbContext.AlertLogs
                .AsNoTracking()
                .Where(a => a.UserId == userId);

            if (!string.IsNullOrWhiteSpace(alertType))
            {
                query = query.Where(a => a.AlertType == alertType.Trim());
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.AlertType,
                    a.Message,
                    a.ImagePath,
                    a.IsRead,
                    a.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                page,
                pageSize,
                totalCount,
                items
            });
        }
    }
}
