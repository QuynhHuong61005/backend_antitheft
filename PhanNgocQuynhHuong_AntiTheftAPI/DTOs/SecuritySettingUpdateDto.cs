
using System.ComponentModel.DataAnnotations;

namespace PhanNgocQuynhHuong_AntiTheftAPI.Dtos;

public class SecuritySettingUpdateDto
{
    public bool IsArmed { get; set; }

    public bool EnableSoundAlert { get; set; }

    public bool EnableEmailAlert { get; set; }

    [Range(0, 300)]
    public int AlertDelaySeconds { get; set; }
}
