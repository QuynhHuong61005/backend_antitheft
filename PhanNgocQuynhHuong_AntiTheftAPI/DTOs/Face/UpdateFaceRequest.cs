using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Face;

public class UpdateFaceRequest
{
    [MaxLength(100, ErrorMessage = "Tên hồ sơ tối đa 100 ký tự.")]
    public string? ProfileName { get; set; }

    /// <summary>
    /// Ảnh khuôn mặt mới (tùy chọn nếu muốn thay thế ảnh cũ).
    /// </summary>
    public IFormFile? FaceImage { get; set; }

    /// <summary>
    /// Vector đặc trưng mới (tùy chọn nếu muốn thay thế vector cũ).
    /// </summary>
    public string? EmbeddingJson { get; set; }

    public bool? IsActive { get; set; }
}
