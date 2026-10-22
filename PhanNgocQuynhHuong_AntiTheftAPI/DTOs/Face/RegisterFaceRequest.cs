using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Face;

public class RegisterFaceRequest
{
    [Required(ErrorMessage = "Tên hồ sơ khuôn mặt không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên hồ sơ tối đa 100 ký tự.")]
    public string ProfileName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ảnh khuôn mặt tham chiếu là bắt buộc.")]
    public IFormFile FaceImage { get; set; } = null!;

    /// <summary>
    /// Vector đặc trưng trích xuất từ mô hình AI trên client (dạng chuỗi JSON array số thực).
    /// </summary>
    public string? EmbeddingJson { get; set; }
}
