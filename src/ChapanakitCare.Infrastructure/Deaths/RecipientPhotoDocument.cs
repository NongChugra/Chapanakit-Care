using ChapanakitCare.Infrastructure.Members;

namespace ChapanakitCare.Infrastructure.Deaths;

public sealed record RecipientPhotoDocument(int BeneficiarySlotNo, string FileName, string ContentType, byte[] Bytes)
{
    public const int MaximumBytes = 10 * 1024 * 1024;

    internal RecipientPhotoDocument Validate()
    {
        if (BeneficiarySlotNo is < 1 or > 2 || Bytes.Length is 0 or > MaximumBytes)
            throw new MemberValidationException("ภาพผู้รับเงินต้องมีขนาดไม่เกิน 10 MB และระบุผู้รับเงินให้ถูกต้อง");
        var png = Bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = Bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 });
        if (!png && !jpeg)
            throw new MemberValidationException("ภาพผู้รับเงินต้องเป็นไฟล์ JPG หรือ PNG ที่ถูกต้อง");
        try
        {
            using var decoded = QuestPDF.Infrastructure.Image.FromBinaryData(Bytes);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new MemberValidationException("ไม่สามารถอ่านภาพผู้รับเงิน กรุณาเลือกไฟล์ JPG หรือ PNG ที่สมบูรณ์");
        }
        // Names and media types come from validated content, never from upload paths.
        return this with { FileName = $"recipient-{BeneficiarySlotNo}.{(png ? "png" : "jpg")}",
            ContentType = png ? "image/png" : "image/jpeg", Bytes = Bytes.ToArray() };
    }
}
