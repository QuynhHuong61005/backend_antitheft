using System.ComponentModel.DataAnnotations;

namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Admin;

public class UpdateUserRoleRequest
{
    [Required(ErrorMessage = "Vai trò không được để trống.")]
    [RegularExpression("^(Admin|User)$", ErrorMessage = "Vai trò chỉ được là 'Admin' hoặc 'User'.")]
    public string Role { get; set; } = string.Empty;
}
