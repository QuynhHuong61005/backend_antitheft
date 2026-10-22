namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Admin;

public class AdminAccessLogDto
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CapturedImagePath { get; set; }
    public DateTime CreatedAt { get; set; }
}
