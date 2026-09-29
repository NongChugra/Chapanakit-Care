namespace ChapanakitCare.Domain.Entities;

public sealed class DeathRecipientPhoto
{
    public Guid Id { get; set; }
    public Guid DeathCaseId { get; set; }
    public int BeneficiarySlotNo { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Bytes { get; set; } = [];
    public string Sha256 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
