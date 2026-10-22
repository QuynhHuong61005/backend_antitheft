using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FileStorageService> _logger;
    private const long MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png"
    };

    public FileStorageService(IWebHostEnvironment env, ILogger<FileStorageService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<(bool IsSuccess, string? ErrorMessage, string? RelativePath)> SaveImageAsync(IFormFile file, string subFolder)
    {
        if (file == null || file.Length == 0)
        {
            return (false, "File ảnh không được rỗng.", null);
        }

        if (file.Length > MaxFileSizeInBytes)
        {
            return (false, "Kích thước ảnh vượt quá giới hạn cho phép (tối đa 5 MB).", null);
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return (false, "Định dạng file không được hỗ trợ. Chỉ chấp nhận các định dạng .jpg, .jpeg, .png.", null);
        }

        // Validate Magic Bytes để ngăn chặn file giả mạo định dạng
        try
        {
            using var stream = file.OpenReadStream();
            var header = new byte[8];
            var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));
            if (bytesRead < 4)
            {
                return (false, "File không hợp lệ hoặc kích thước quá nhỏ.", null);
            }

            var isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            var isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;

            if (!isJpeg && !isPng)
            {
                return (false, "Nội dung file không đúng định dạng ảnh JPEG hoặc PNG hợp lệ.", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi kiểm tra header file ảnh.");
            return (false, "Không thể đọc và xác thực file ảnh.", null);
        }

        // Chuẩn hóa tên thư mục và tên file
        var cleanSubFolder = subFolder.Replace("..", "").Trim('/', '\\');
        var uploadsRoot = Path.Combine(_env.ContentRootPath, "Uploads", cleanSubFolder);
        if (!Directory.Exists(uploadsRoot))
        {
            Directory.CreateDirectory(uploadsRoot);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPhysicalPath = Path.Combine(uploadsRoot, uniqueFileName);

        try
        {
            using (var destinationStream = new FileStream(fullPhysicalPath, FileMode.Create))
            {
                await file.CopyToAsync(destinationStream);
            }

            // Trả về relative path dạng uploads/faces/...
            var relativePath = Path.Combine("Uploads", cleanSubFolder, uniqueFileName).Replace("\\", "/");
            return (true, null, relativePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu file vào ổ đĩa.");
            return (false, "Có lỗi xảy ra trong quá trình lưu trữ file ảnh.", null);
        }
    }

    public bool DeleteFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;

        try
        {
            var cleanPath = relativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var fullPhysicalPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, cleanPath));
            var uploadsRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Uploads"));

            // Đảm bảo không bị path traversal ra ngoài thư mục Uploads
            if (!fullPhysicalPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Phát hiện đường dẫn không hợp lệ khi xóa file: {Path}", relativePath);
                return false;
            }

            if (File.Exists(fullPhysicalPath))
            {
                File.Delete(fullPhysicalPath);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa file vật lý.");
        }

        return false;
    }

    public (bool Exists, string PhysicalPath, string ContentType) GetPhysicalFileInfo(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return (false, string.Empty, "application/octet-stream");

        var cleanPath = relativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var fullPhysicalPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, cleanPath));
        var uploadsRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Uploads"));

        if (!fullPhysicalPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPhysicalPath))
        {
            return (false, string.Empty, "application/octet-stream");
        }

        var extension = Path.GetExtension(fullPhysicalPath).ToLowerInvariant();
        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };

        return (true, fullPhysicalPath, contentType);
    }
}
