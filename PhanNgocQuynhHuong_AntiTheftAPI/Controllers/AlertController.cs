using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhanNgocQuynhHuong_AntiTheftAPI.Data;
using PhanNgocQuynhHuong_AntiTheftAPI.Models;
using PhanNgocQuynhHuong_AntiTheftAPI.Services;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AlertController : ControllerBase
    {
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
                return StatusCode(500, new
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
                return StatusCode(500, new
                {
                    success = false,
                    message =
                        "Đã lưu lịch sử cảnh báo nhưng gửi email thất bại.",
                    alertLogId = alertLog.Id
                });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
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
