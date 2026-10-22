namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Admin;

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int AdminCount { get; set; }
    public int UserCount { get; set; }
    public int LockedUsersCount { get; set; }
    public int TotalAlerts { get; set; }
    public int TodayAlertsCount { get; set; }
    public int UnreadAlertsCount { get; set; }
    public int ArmedDevicesCount { get; set; }
    public List<AlertTypeStatDto> AlertsByType { get; set; } = new();
    public List<DailyAlertStatDto> RecentAlerts7Days { get; set; } = new();
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
}

public class AlertTypeStatDto
{
    public string AlertType { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DailyAlertStatDto
{
    public string Date { get; set; } = string.Empty; // Format yyyy-MM-dd
    public int Count { get; set; }
}
