using System.ComponentModel.DataAnnotations;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Models;

public class SecuritySetting
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public bool IsArmed { get; set; } = false;

    public bool EnableSoundAlert { get; set; } = true;

    public bool EnableEmailAlert { get; set; } = true;

    public int AlertDelaySeconds { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
}