namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Admin;

public class AdminAlertItemDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserFullName { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
