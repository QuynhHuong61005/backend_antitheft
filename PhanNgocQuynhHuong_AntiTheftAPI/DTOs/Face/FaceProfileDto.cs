namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Face;

public class FaceProfileDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool HasEmbedding { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
