using Microsoft.AspNetCore.Http;

namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Face;

public class VerifyFaceRequest
{
    /// <summary>
    /// Ảnh chụp từ camera tại thời điểm xác minh.
    /// </summary>
    public IFormFile? CapturedImage { get; set; }

    /// <summary>
    /// Vector đặc trưng trích xuất từ khung hình camera của người đang đứng trước máy.
    /// </summary>
    public string? EmbeddingJson { get; set; }
}
