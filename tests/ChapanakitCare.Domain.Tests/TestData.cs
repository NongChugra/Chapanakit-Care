using ChapanakitCare.Domain.Entities;

namespace ChapanakitCare.Domain.Tests;

internal static class TestData
{
    private static readonly Guid MemberId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);

    internal static Member Member() => new()
    {
        Id = MemberId,
        RunNo = "00001",
        FirstName = "ทดสอบ",
        LastName = "สมาชิก",
        District = "ร้องกวาง",
        Province = "แพร่",
        ApplicationDate = new DateOnly(2026, 8, 25),
        ApprovalDate = new DateOnly(2026, 8, 25),
        CoverageStartDate = new DateOnly(2027, 2, 21),
        Status = MemberStatus.Normal,
        AdvanceUnitsBalance = 30,
        Version = 1,
        CreatedAtUtc = Now,
        CreatedBy = "test",
        UpdatedAtUtc = Now,
        UpdatedBy = "test"
    };

    internal static MemberBeneficiary Beneficiary(int slotNo) => new()
    {
        Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        MemberId = MemberId,
        SlotNo = slotNo,
        FirstName = "ผู้รับ",
        LastName = "ทดสอบ",
        IsActive = true,
        Version = 1,
        CreatedAtUtc = Now,
        CreatedBy = "test",
        UpdatedAtUtc = Now,
        UpdatedBy = "test"
    };
}

