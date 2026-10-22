using Microsoft.AspNetCore.Http;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Services;

public interface IFileStorageService
{
    Task<(bool IsSuccess, string? ErrorMessage, string? RelativePath)> SaveImageAsync(IFormFile file, string subFolder);
    bool DeleteFile(string? relativePath);
    (bool Exists, string PhysicalPath, string ContentType) GetPhysicalFileInfo(string relativePath);
}
