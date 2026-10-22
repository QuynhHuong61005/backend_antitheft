using System.ComponentModel.DataAnnotations;

namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "Họ tên không được để trống.")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    public string Password { get; set; } = string.Empty;
}