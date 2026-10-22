namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Admin;

public class SystemInfoDto
{
    public string Environment { get; set; } = string.Empty;
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
    public string JwtIssuer { get; set; } = string.Empty;
    public string JwtAudience { get; set; } = string.Empty;
    public bool EmailServiceConfigured { get; set; }
    public bool DatabaseConnected { get; set; }
    public int TotalUsers { get; set; }
    public int TotalAlerts { get; set; }
    public int TotalSecuritySettings { get; set; }
}
