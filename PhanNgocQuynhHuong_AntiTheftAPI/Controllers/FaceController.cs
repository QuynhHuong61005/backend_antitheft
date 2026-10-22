using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhanNgocQuynhHuong_AntiTheftAPI.Data;
using PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Face;
using PhanNgocQuynhHuong_AntiTheftAPI.Models;
using PhanNgocQuynhHuong_AntiTheftAPI.Services;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FaceController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _fileStorageService;
    private readonly IFaceRecognitionService _faceRecognitionService;
    private readonly ILogger<FaceController> _logger;

    public FaceController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IFileStorageService fileStorageService,
        IFaceRecognitionService faceRecognitionService,
        ILogger<FaceController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _fileStorageService = fileStorageService;
        _faceRecognitionService = faceRecognitionService;
        _logger = logger;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
    }

    private static FaceProfileDto MapToDto(FaceProfile profile)
    {
        return new FaceProfileDto
        {
            Id = profile.Id,
            UserId = profile.UserId,
            ProfileName = profile.ProfileName,
            ImageUrl = !string.IsNullOrWhiteSpace(profile.FaceImagePath)
                ? $"/api/face/image/{profile.FaceImagePath}"
                : null,
            HasEmbedding = !string.IsNullOrWhiteSpace(profile.FaceEmbeddingJson),
            IsActive = profile.IsActive,
            CreatedAt = profile.CreatedAt
        };
    }

    // ==========================================
    // 1. LẤY TẤT CẢ HỒ SƠ KHUÔN MẶT CỦA USER
    // ==========================================
    [HttpGet("profiles")]
    public async Task<IActionResult> GetProfiles()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { success = false, message = "Không xác định được danh tính người dùng từ token." });

        var profiles = await _dbContext.FaceProfiles
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        var list = profiles.Select(MapToDto).ToList();

        return Ok(new
        {
            success = true,
            total = list.Count,
            data = list
        });
    }

    // ==========================================
    // 1B. LẤY HỒ SƠ KHUÔN MẶT (TƯƠNG THÍCH NGƯỢC HOẶC THEO ID)
    // ==========================================
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile([FromQuery] int? id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { success = false, message = "Không xác định được danh tính người dùng từ token." });

        FaceProfile? profile;
        if (id.HasValue && id.Value > 0)
        {
            profile = await _dbContext.FaceProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id.Value && f.UserId == userId);
        }
        else
        {
            // Mặc định lấy hồ sơ đầu tiên
            profile = await _dbContext.FaceProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.UserId == userId);
        }

        if (profile == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Chưa có hồ sơ khuôn mặt nào được đăng ký cho tài khoản này."
            });
        }

        return Ok(new
        {
            success = true,
            data = MapToDto(profile)
        });
    }

    // ==========================================
    // 2. ĐĂNG KÝ HỒ SƠ KHUÔN MẶT MỚI (HỖ TRỢ NHIỀU NGƯỜI)
    // ==========================================
    [HttpPost("register")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RegisterFace([FromForm] RegisterFaceRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { success = false, message = "Không xác định được danh tính người dùng từ token." });

        if (request == null)
            return BadRequest(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });

        var trimmedName = request.ProfileName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return BadRequest(new { success = false, message = "Tên người được phép không được để trống." });
        }

        // Kiểm tra xem đã có hồ sơ trùng tên trong tài khoản chưa
        var nameConflict = await _dbContext.FaceProfiles
            .AnyAsync(f => f.UserId == userId && f.ProfileName.ToLower() == trimmedName.ToLower());

        if (nameConflict)
        {
            return Conflict(new
            {
                success = false,
                message = $"Hồ sơ với tên '{trimmedName}' đã tồn tại trong danh sách. Vui lòng đặt tên phân biệt."
            });
        }

        // Validate dữ liệu Embedding nếu có gửi kèm
        if (!string.IsNullOrWhiteSpace(request.EmbeddingJson))
        {
            var (isValid, errMsg, _) = _faceRecognitionService.ParseAndValidateEmbedding(request.EmbeddingJson);
            if (!isValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Vector đặc trưng không hợp lệ: {errMsg}"
                });
            }
        }

        // Lưu ảnh khuôn mặt an toàn
        var (isSaveSuccess, saveError, relativePath) = await _fileStorageService.SaveImageAsync(request.FaceImage, "faces");
        if (!isSaveSuccess || string.IsNullOrWhiteSpace(relativePath))
        {
            return BadRequest(new
            {
                success = false,
                message = saveError ?? "Không thể lưu file ảnh khuôn mặt."
            });
        }

        var newProfile = new FaceProfile
        {
            UserId = userId,
            ProfileName = trimmedName,
            FaceImagePath = relativePath,
            FaceEmbeddingJson = request.EmbeddingJson?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _dbContext.FaceProfiles.Add(newProfile);
            await _dbContext.SaveChangesAsync();

            // Ghi nhận nhật ký truy cập
            _dbContext.AccessLogs.Add(new AccessLog
            {
                UserId = userId,
                EventType = "FaceProfileRegistered",
                Description = $"Đăng ký người được phép '{newProfile.ProfileName}' thành công.",
                CapturedImagePath = relativePath,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                message = "Đăng ký hồ sơ khuôn mặt thành công.",
                data = MapToDto(newProfile)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi cơ sở dữ liệu khi đăng ký hồ sơ khuôn mặt.");
            _fileStorageService.DeleteFile(relativePath);

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Lỗi hệ thống khi lưu hồ sơ khuôn mặt vào cơ sở dữ liệu."
            });
        }
    }

    // ==========================================
    // 3. CẬP NHẬT HỒ SƠ KHUÔN MẶT THEO ID HOẶC TƯƠNG THÍCH CŨ
    // ==========================================
    [HttpPut("profile/{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateProfileById(int id, [FromForm] UpdateFaceRequest request)
    {
        return await ProcessUpdateProfile(id, request);
    }

    [HttpPut("profile")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateProfileDefault([FromQuery] int? id, [FromForm] UpdateFaceRequest request)
    {
        return await ProcessUpdateProfile(id, request);
    }

    private async Task<IActionResult> ProcessUpdateProfile(int? profileId, UpdateFaceRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { success = false, message = "Không xác định được danh tính người dùng từ token." });

        FaceProfile? profile;
        if (profileId.HasValue && profileId.Value > 0)
        {
            profile = await _dbContext.FaceProfiles
                .FirstOrDefaultAsync(f => f.Id == profileId.Value && f.UserId == userId);
        }
        else
        {
            profile = await _dbContext.FaceProfiles
                .FirstOrDefaultAsync(f => f.UserId == userId);
        }

        if (profile == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Không tìm thấy hồ sơ khuôn mặt yêu cầu để cập nhật."
            });
        }

        // Cập nhật tên hồ sơ nếu có
        if (!string.IsNullOrWhiteSpace(request.ProfileName))
        {
            var trimmedName = request.ProfileName.Trim();
            // Kiểm tra trùng tên với profile khác của cùng user
            var conflict = await _dbContext.FaceProfiles
                .AnyAsync(f => f.UserId == userId && f.Id != profile.Id && f.ProfileName.ToLower() == trimmedName.ToLower());
            if (conflict)
            {
                return Conflict(new
                {
                    success = false,
                    message = $"Tên '{trimmedName}' đã được dùng cho hồ sơ khác. Vui lòng chọn tên khác."
                });
            }
            profile.ProfileName = trimmedName;
        }

        // Cập nhật trạng thái kích hoạt nếu có
        if (request.IsActive.HasValue)
        {
            profile.IsActive = request.IsActive.Value;
        }

        // Cập nhật vector embedding nếu có
        if (!string.IsNullOrWhiteSpace(request.EmbeddingJson))
        {
            var (isValid, errMsg, _) = _faceRecognitionService.ParseAndValidateEmbedding(request.EmbeddingJson);
            if (!isValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Vector đặc trưng không hợp lệ: {errMsg}"
                });
            }

            profile.FaceEmbeddingJson = request.EmbeddingJson.Trim();
        }

        // Cập nhật ảnh nếu có file mới gửi lên
        string? oldImagePath = null;
        if (request.FaceImage != null && request.FaceImage.Length > 0)
        {
            var (isSaveSuccess, saveError, newRelativePath) = await _fileStorageService.SaveImageAsync(request.FaceImage, "faces");
            if (!isSaveSuccess || string.IsNullOrWhiteSpace(newRelativePath))
            {
                return BadRequest(new
                {
                    success = false,
                    message = saveError ?? "Không thể lưu file ảnh khuôn mặt mới."
                });
            }

            oldImagePath = profile.FaceImagePath;
            profile.FaceImagePath = newRelativePath;
        }

        try
        {
            await _dbContext.SaveChangesAsync();

            // Nếu lưu DB thành công và có ảnh mới, xóa file ảnh cũ
            if (!string.IsNullOrWhiteSpace(oldImagePath))
            {
                _fileStorageService.DeleteFile(oldImagePath);
            }

            // Ghi nhận nhật ký truy cập
            _dbContext.AccessLogs.Add(new AccessLog
            {
                UserId = userId,
                EventType = "FaceProfileUpdated",
                Description = $"Cập nhật hồ sơ người được phép '{profile.ProfileName}' thành công.",
                CapturedImagePath = profile.FaceImagePath,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Cập nhật hồ sơ khuôn mặt thành công.",
                data = MapToDto(profile)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi cơ sở dữ liệu khi cập nhật hồ sơ khuôn mặt.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Lỗi hệ thống khi cập nhật hồ sơ khuôn mặt vào cơ sở dữ liệu."
            });
        }
    }

    // ==========================================
    // 4. XÓA HỒ SƠ KHUÔN MẶT THEO ID HOẶC TƯƠNG THÍCH CŨ
    // ==========================================
    [HttpDelete("profile/{id:int}")]
    public async Task<IActionResult> DeleteProfileById(int id)
    {
        return await ProcessDeleteProfile(id);
    }

    [HttpDelete("profile")]
    public async Task<IActionResult> DeleteProfileDefault([FromQuery] int? id)
    {
        return await ProcessDeleteProfile(id);
    }

    private async Task<IActionResult> ProcessDeleteProfile(int? profileId)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { success = false, message = "Không xác định được danh tính người dùng từ token." });

        FaceProfile? profile;
        if (profileId.HasValue && profileId.Value > 0)
        {
            profile = await _dbContext.FaceProfiles
                .FirstOrDefaultAsync(f => f.Id == profileId.Value && f.UserId == userId);
        }
        else
        {
            profile = await _dbContext.FaceProfiles
                .FirstOrDefaultAsync(f => f.UserId == userId);
        }

        if (profile == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Không tìm thấy hồ sơ khuôn mặt yêu cầu để xóa."
            });
        }

        var imagePathToDelete = profile.FaceImagePath;
        var profileName = profile.ProfileName;

        try
        {
            _dbContext.FaceProfiles.Remove(profile);

            // Ghi nhận nhật ký truy cập
            _dbContext.AccessLogs.Add(new AccessLog
            {
                UserId = userId,
                EventType = "FaceProfileDeleted",
                Description = $"Đã xóa hồ sơ người được phép '{profileName}'.",
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            // Xóa file ảnh vật lý
            if (!string.IsNullOrWhiteSpace(imagePathToDelete))
            {
                _fileStorageService.DeleteFile(imagePathToDelete);
            }

            return Ok(new
            {
                success = true,
                message = $"Đã xóa hồ sơ '{profileName}' thành công."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi cơ sở dữ liệu khi xóa hồ sơ khuôn mặt.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Lỗi hệ thống khi xóa hồ sơ khuôn mặt."
            });
        }
    }

    // ==========================================
    // 5. XÁC MINH KHUÔN MẶT TỪ CAMERA (1:N MATCHING)
    // ==========================================
    [HttpPost("verify")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> VerifyFace([FromForm] VerifyFaceRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { success = false, message = "Không xác định được danh tính người dùng từ token." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Unauthorized(new { success = false, message = "Tài khoản người dùng không tồn tại." });

        // Lấy tất cả hồ sơ khuôn mặt của tài khoản
        var profiles = await _dbContext.FaceProfiles
            .Where(f => f.UserId == userId)
            .ToListAsync();

        if (profiles.Count == 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Tài khoản của bạn chưa đăng ký hồ sơ khuôn mặt nào để làm dữ liệu đối chiếu xác minh."
            });
        }

        var activeProfiles = profiles.Where(p => p.IsActive).ToList();
        if (activeProfiles.Count == 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Tất cả hồ sơ người được phép của bạn đều đang Tắt (Inactive). Vui lòng kích hoạt ít nhất một hồ sơ."
            });
        }

        // Lưu ảnh chụp camera nếu có gửi kèm (làm bằng chứng)
        string? capturedImagePath = null;
        if (request?.CapturedImage != null && request.CapturedImage.Length > 0)
        {
            var (isSaveSuccess, saveError, relativePath) = await _fileStorageService.SaveImageAsync(request.CapturedImage, "captures");
            if (isSaveSuccess)
            {
                capturedImagePath = relativePath;
            }
            else
            {
                _logger.LogWarning("Không thể lưu ảnh chụp camera lúc xác minh: {Error}", saveError);
            }
        }

        // Thực hiện so sánh 1:N với danh sách người được phép qua FaceRecognitionService
        var verificationResult = await _faceRecognitionService.VerifyFace1ToNAsync(
            user,
            activeProfiles,
            request ?? new VerifyFaceRequest(),
            capturedImagePath);

        return Ok(new
        {
            success = true,
            data = verificationResult
        });
    }

    // ==========================================
    // 6. XEM/TẢI ẢNH KHUÔN MẶT (BẢO VỆ PHÂN QUYỀN)
    // ==========================================
    [HttpGet("image/{*relativePath}")]
    public async Task<IActionResult> GetImage(string relativePath)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var isAdmin = User.IsInRole("Admin");

        // Chuẩn hóa relative path
        var normalizedPath = relativePath.Replace("\\", "/").TrimStart('/');

        // Kiểm tra quyền sở hữu ảnh:
        // Người dùng thường chỉ được xem ảnh thuộc về FaceProfile, AccessLog hoặc AlertLog của chính mình
        if (!isAdmin)
        {
            var isOwnFace = await _dbContext.FaceProfiles
                .AnyAsync(f => f.UserId == userId && f.FaceImagePath != null && f.FaceImagePath.Replace("\\", "/") == normalizedPath);

            var isOwnAccess = !isOwnFace && await _dbContext.AccessLogs
                .AnyAsync(a => a.UserId == userId && a.CapturedImagePath != null && a.CapturedImagePath.Replace("\\", "/") == normalizedPath);

            var isOwnAlert = !isOwnFace && !isOwnAccess && await _dbContext.AlertLogs
                .AnyAsync(a => a.UserId == userId && a.ImagePath != null && a.ImagePath.Replace("\\", "/") == normalizedPath);

            if (!isOwnFace && !isOwnAccess && !isOwnAlert)
            {
                _logger.LogWarning("Cảnh báo truy cập trái phép ảnh sinh trắc học: User {UserId} cố truy cập file {Path}", userId, normalizedPath);
                return Forbid();
            }
        }

        var (exists, physicalPath, contentType) = _fileStorageService.GetPhysicalFileInfo(normalizedPath);
        if (!exists)
        {
            return NotFound(new { message = "Không tìm thấy file ảnh yêu cầu." });
        }

        return PhysicalFile(physicalPath, contentType);
    }
}
