using System.ComponentModel.DataAnnotations;

namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs;

/// <summary>
/// DTO nhận dữ liệu sự kiện phát hiện âm thanh bất thường từ thiết bị di động.
/// </summary>
public class SoundAlertRequest
{
    /// <summary>
    /// Mức âm thanh đo được trên thiết bị (decibel - dB).
    /// </summary>
    [Required(ErrorMessage = "Mức âm thanh (SoundLevel) là bắt buộc.")]
    [Range(1.0, 200.0, ErrorMessage = "Mức âm thanh phải nằm trong khoảng từ 1.0 đến 200.0 dB.")]
    public double SoundLevel { get; set; }

    /// <summary>
    /// Ngưỡng âm thanh thiết lập để kích hoạt cảnh báo (decibel - dB). Tùy chọn.
    /// </summary>
    [Range(0.0, 200.0, ErrorMessage = "Ngưỡng âm thanh phải nằm trong khoảng từ 0.0 đến 200.0 dB.")]
    public double? Threshold { get; set; }

    /// <summary>
    /// Thời điểm phát hiện trên thiết bị theo chuẩn UTC. Tùy chọn (mặc định lấy thời gian server nhận request).
    /// </summary>
    public DateTime? DetectedAt { get; set; }

    /// <summary>
    /// Ghi chú hoặc thông tin bổ sung về sự kiện âm thanh. Tùy chọn.
    /// </summary>
    [MaxLength(200, ErrorMessage = "Mô tả không được vượt quá 200 ký tự.")]
    public string? Description { get; set; }
}
