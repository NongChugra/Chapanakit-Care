namespace ChapanakitCare.Domain.Entities;

public sealed class WelfareCollection
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string MemberRunNo { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string GroupNo { get; set; } = string.Empty;
    public string CycleKey { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
    public DateOnly DueDate { get; set; }
    public long AmountSatang { get; set; }
    public string RequestToken { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
