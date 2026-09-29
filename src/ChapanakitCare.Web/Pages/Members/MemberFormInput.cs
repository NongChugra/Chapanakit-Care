using System.ComponentModel.DataAnnotations;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Members;

namespace ChapanakitCare.Web.Pages.Members;

public sealed class MemberFormInput
{
    public int Version { get; set; }
    public string? Title { get; set; }
    [Required(ErrorMessage = "กรุณากรอกชื่อ")]
    public string FirstName { get; set; } = string.Empty;
    [Required(ErrorMessage = "กรุณากรอกนามสกุล")]
    public string LastName { get; set; } = string.Empty;
    public string? Gender { get; set; }
    [RegularExpression("^[0-9]{13}$", ErrorMessage = "เลขประจำตัวประชาชนต้องมี 13 หลัก")]
    public string? PersonalIdCard { get; set; }
    [Validation.GregorianDate]
    public DateOnly? BirthDate { get; set; }
    public string? HouseNo { get; set; }
    public string? Under { get; set; }
    public string? Moo { get; set; }
    public string? Subdistrict { get; set; }
    public string? District { get; set; } = "ร้องกวาง";
    public string? Province { get; set; } = "แพร่";
    [RegularExpression("^[0-9]{5}$", ErrorMessage = "รหัสไปรษณีย์ต้องมี 5 หลัก")]
    public string? PostalCode { get; set; }
    [RegularExpression("^[0-9]{10}$", ErrorMessage = "โทรศัพท์ต้องเป็นตัวเลข 10 หลัก")]
    public string? Mobile { get; set; }
    public string? GroupNo { get; set; }
    [Required(ErrorMessage = "กรุณากรอกวันที่สมัคร")]
    [Validation.GregorianDate]
    public DateOnly ApplicationDate { get; set; }
    [Required(ErrorMessage = "กรุณากรอกวันอนุมัติ")]
    [Validation.GregorianDate]
    public DateOnly ApprovalDate { get; set; }
    public BeneficiaryInput Beneficiary1 { get; set; } = new() { SlotNo = 1 };
    public BeneficiaryInput Beneficiary2 { get; set; } = new() { SlotNo = 2 };

    public IReadOnlyList<BeneficiaryCommand> ToBeneficiaries() =>
        new[] { Beneficiary1, Beneficiary2 }
            .Where(value => value.IsStarted())
            .Select(value => value.UseMemberAddress
                ? value.ToCommand() with { HouseNo = HouseNo, Under = Under, Moo = Moo, Subdistrict = Subdistrict,
                    District = District, Province = Province, PostalCode = PostalCode }
                : value.ToCommand())
            .ToArray();

    public RegisterMemberCommand ToRegisterCommand() => new(
        Title, FirstName, LastName, Gender, PersonalIdCard, BirthDate, HouseNo, Under, Moo,
        Subdistrict, PostalCode, Mobile, GroupNo, ApplicationDate, ApprovalDate, ToBeneficiaries(), District, Province);

    public UpdateMemberCommand ToUpdateCommand() => new(
        Version, Title, FirstName, LastName, Gender, PersonalIdCard, BirthDate, HouseNo, Under,
        Moo, Subdistrict, PostalCode, Mobile, GroupNo, ApplicationDate, ApprovalDate, ToBeneficiaries(), District, Province);

    public static MemberFormInput From(Member member, IReadOnlyList<MemberBeneficiary> beneficiaries)
    {
        var first = beneficiaries.SingleOrDefault(value => value.SlotNo == 1);
        var second = beneficiaries.SingleOrDefault(value => value.SlotNo == 2);
        return new MemberFormInput
        {
            Version = member.Version,
            Title = member.Title,
            FirstName = member.FirstName,
            LastName = member.LastName,
            Gender = member.Gender,
            PersonalIdCard = member.PersonalIdCard,
            BirthDate = member.BirthDate,
            HouseNo = member.HouseNo,
            Under = member.Under,
            Moo = member.Moo,
            Subdistrict = member.Subdistrict,
            District = member.District,
            Province = member.Province,
            PostalCode = member.PostalCode,
            Mobile = member.Mobile,
            GroupNo = member.GroupNo,
            ApplicationDate = member.ApplicationDate,
            ApprovalDate = member.ApprovalDate,
            Beneficiary1 = BeneficiaryInput.From(first, 1),
            Beneficiary2 = BeneficiaryInput.From(second, 2)
        };
    }
}

public sealed class BeneficiaryInput
{
    public bool UseMemberAddress { get; set; }
    public int SlotNo { get; set; }
    public string? Title { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Relationship { get; set; }
    [RegularExpression("^[0-9]{13}$", ErrorMessage = "เลขประจำตัวประชาชนผู้รับเงินต้องเป็นตัวเลข 13 หลัก")]
    public string? PersonalIdCard { get; set; }
    [RegularExpression("^[0-9]{10}$", ErrorMessage = "โทรศัพท์ผู้รับเงินต้องเป็นตัวเลข 10 หลัก")]
    public string? Mobile { get; set; }
    public string? HouseNo { get; set; }
    public string? Under { get; set; }
    public string? Moo { get; set; }
    public string? Subdistrict { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }

    public bool IsStarted() => new[] { Title, FirstName, LastName, Relationship, PersonalIdCard, Mobile, HouseNo, Under, Moo, Subdistrict, District, Province, PostalCode }
        .Any(value => !string.IsNullOrWhiteSpace(value));

    public BeneficiaryCommand ToCommand() => new(
        SlotNo, Title, FirstName?.Trim() ?? string.Empty, LastName?.Trim() ?? string.Empty,
        Relationship, PersonalIdCard, Mobile, HouseNo, Under, Moo, Subdistrict, District, Province, PostalCode);

    public static BeneficiaryInput From(MemberBeneficiary? value, int slotNo) => value is null
        ? new BeneficiaryInput { SlotNo = slotNo }
        : new BeneficiaryInput
        {
            SlotNo = slotNo,
            Title = value.Title,
            FirstName = value.FirstName,
            LastName = value.LastName,
            Relationship = value.Relationship,
            PersonalIdCard = value.PersonalIdCard,
            Mobile = value.Mobile,
            HouseNo = value.HouseNo,
            Under = value.Under,
            Moo = value.Moo,
            Subdistrict = value.Subdistrict,
            District = value.District,
            Province = value.Province,
            PostalCode = value.PostalCode
        };
}
