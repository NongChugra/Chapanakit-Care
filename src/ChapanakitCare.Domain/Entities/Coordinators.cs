namespace ChapanakitCare.Domain.Entities;

public sealed class CoordinatorPosition
{
    public string PositionKey { get; set; } = "";
    public string RoleCode { get; set; } = "";
    public string? GroupNo { get; set; }
    public Guid? MemberId { get; set; }
    public DateOnly? AppointedOn { get; set; }
    public int Version { get; set; }
}

public sealed class CoordinatorEvent
{
    public Guid Id { get; set; }
    public string PositionKey { get; set; } = "";
    public string RoleCode { get; set; } = "";
    public string? GroupNo { get; set; }
    public string Action { get; set; } = "";
    public Guid? PreviousMemberId { get; set; }
    public string? PreviousMemberName { get; set; }
    public string? PreviousRunNo { get; set; }
    public Guid? MemberId { get; set; }
    public string? MemberName { get; set; }
    public string? MemberRunNo { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Actor { get; set; } = "";
    public string? Reason { get; set; }
    public int PositionVersion { get; set; }
}
