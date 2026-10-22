using System.ComponentModel.DataAnnotations;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Models;

public class AccessLog
{
    public int Id { get; set; }

    // Có thể null nếu sự kiện chưa xác định được người dùng.
    public string? UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? CapturedImagePath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
}