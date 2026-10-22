namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Face;

public class FaceVerificationResultDto
{
    public bool IsMatched { get; set; }
    public double Similarity { get; set; }
    public double Confidence { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? CapturedImageUrl { get; set; }
    public DateTime VerifiedAt { get; set; }
    public bool SecurityAlertTriggered { get; set; }

    // Thông tin nhận diện đa khuôn mặt (1:N)
    public int? MatchedProfileId { get; set; }
    public string? MatchedProfileName { get; set; }
}
