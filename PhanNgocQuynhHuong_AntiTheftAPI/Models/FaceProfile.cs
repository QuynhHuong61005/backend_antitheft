using System.ComponentModel.DataAnnotations;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Models;

public class FaceProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ProfileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? FaceImagePath { get; set; }

    // Có thể lưu dữ liệu đặc trưng khuôn mặt dạng JSON
    // nếu kiến trúc nhận diện sau này cần sử dụng.
    public string? FaceEmbeddingJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
}