using ChapanakitCare.Infrastructure.Members;

namespace ChapanakitCare.Domain.Tests;

public sealed class CheckpointThreeFormGuardTests
{
    [Theory]
    [InlineData("นาย", "ชาย")]
    [InlineData("เด็กชาย", "ชาย")]
    [InlineData("นาง", "หญิง")]
    [InlineData("นางสาว", "หญิง")]
    [InlineData("น.ส.", "หญิง")]
    [InlineData("ดร.", null)]
    public void Title_suggests_gender_without_making_unknown_titles_invalid(string title, string? expected)
    {
        Assert.Equal(expected, TitleGenderPolicy.Suggest(title));
    }

    [Fact]
    public void Interactive_registration_rejects_every_missing_member_field()
    {
        var command = CompleteRegistration() with
        {
            Title = null,
            Gender = null,
            PersonalIdCard = null,
            BirthDate = null,
            HouseNo = null,
            Under = null,
            Moo = null,
            Subdistrict = null,
            District = null,
            Province = null,
            PostalCode = null,
            Mobile = null,
            GroupNo = null
        };

        var errors = InteractiveMemberRegistrationPolicy.Validate(command);

        Assert.Equal(
            ["Title", "Gender", "PersonalIdCard", "BirthDate", "HouseNo", "Under", "Moo", "Subdistrict", "District", "Province", "PostalCode", "Mobile", "GroupNo"],
            errors.Select(value => value.Field).ToArray());
    }

    [Fact]
    public void Interactive_registration_requires_one_complete_beneficiary()
    {
        var command = CompleteRegistration() with
        {
            Beneficiaries =
            [
                CompleteBeneficiary(1) with { Mobile = null },
                CompleteBeneficiary(2) with { FirstName = "" }
            ]
        };

        var errors = InteractiveMemberRegistrationPolicy.Validate(command);

        Assert.Contains(errors, value => value.Field == "Beneficiary1.Mobile");
        Assert.Contains(errors, value => value.Field == "Beneficiary2.FirstName");
    }

    private static RegisterMemberCommand CompleteRegistration() => new(
        "นาย", "สมาชิก", "ครบถ้วน", "ชาย", "1111111111111", new DateOnly(1980, 1, 1),
        "11", "บ้านร้องกวาง", "1", "ร้องกวาง", "54140", "0811111111", "001",
        new DateOnly(2026, 8, 25), new DateOnly(2026, 8, 25), [CompleteBeneficiary(1)], "ร้องกวาง", "แพร่");

    private static BeneficiaryCommand CompleteBeneficiary(int slot) => new(
        slot, "นาง", "ผู้รับ", $"คนที่{slot}", "บุตร", "2222222222222", "0822222222",
        "12", "บ้านร้องกวาง", "2", "ร้องกวาง", "ร้องกวาง", "แพร่", "54140");
}
