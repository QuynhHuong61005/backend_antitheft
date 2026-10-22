using System.ComponentModel.DataAnnotations;

namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Admin;

public class LockUserRequest
{
    /// <summary>
    /// Số ngày khóa tài khoản. Nếu để trống hoặc null, mặc định khóa dài hạn (100 năm).
    /// </summary>
    [Range(1, 36500, ErrorMessage = "Số ngày khóa phải từ 1 đến 36500.")]
    public int? Days { get; set; }

    [MaxLength(200, ErrorMessage = "Lý do khóa không vượt quá 200 ký tự.")]
    public string? Reason { get; set; }
}
