namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Admin;

public class AdminUserDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public bool EmailConfirmed { get; set; }
    public bool IsLocked { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; }
    public int AccessFailedCount { get; set; }
    public int TotalAlerts { get; set; }
    public AdminUserSecuritySettingDto? SecuritySetting { get; set; }
    public List<AdminAlertItemDto> RecentAlerts { get; set; } = new();
}

public class AdminUserSecuritySettingDto
{
    public int Id { get; set; }
    public bool IsArmed { get; set; }
    public bool EnableSoundAlert { get; set; }
    public bool EnableEmailAlert { get; set; }
    public int AlertDelaySeconds { get; set; }
    public DateTime UpdatedAt { get; set; }
}
