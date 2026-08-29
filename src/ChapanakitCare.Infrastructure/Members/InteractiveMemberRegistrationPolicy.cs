namespace ChapanakitCare.Infrastructure.Members;

public sealed record RegistrationValidationIssue(string Field, string Message);

public static class TitleGenderPolicy
{
    public static string? Suggest(string? title) => title?.Trim() switch
    {
        "นาย" or "เด็กชาย" => "ชาย",
        "นาง" or "นางสาว" or "น.ส." or "เด็กหญิง" => "หญิง",
        _ => null
    };
}

public static class InteractiveMemberRegistrationPolicy
{
    public static IReadOnlyList<RegistrationValidationIssue> Validate(RegisterMemberCommand command)
    {
        var issues = new List<RegistrationValidationIssue>();
        Required(issues, "Title", command.Title, "กรุณากรอกคำนำหน้า");
        Required(issues, "FirstName", command.FirstName, "กรุณากรอกชื่อ");
        Required(issues, "LastName", command.LastName, "กรุณากรอกนามสกุล");
        Required(issues, "Gender", command.Gender, "กรุณาเลือกเพศ");
        Required(issues, "PersonalIdCard", command.PersonalIdCard, "กรุณากรอกเลขประจำตัวประชาชน");
        if (command.BirthDate is null)
        {
            issues.Add(new("BirthDate", "กรุณากรอกวันเกิด"));
        }
        else if (command.BirthDate > command.ApplicationDate)
        {
            issues.Add(new("BirthDate", "วันเกิดต้องไม่อยู่หลังวันที่สมัคร"));
        }

        Required(issues, "HouseNo", command.HouseNo, "กรุณากรอกบ้านเลขที่");
        Required(issues, "Under", command.Under, "กรุณากรอกสังกัด/ใต้");
        Required(issues, "Moo", command.Moo, "กรุณากรอกหมู่");
        Required(issues, "Subdistrict", command.Subdistrict, "กรุณากรอกตำบล");
        Required(issues, "District", command.District, "กรุณากรอกอำเภอ");
        Required(issues, "Province", command.Province, "กรุณากรอกจังหวัด");
        Required(issues, "PostalCode", command.PostalCode, "กรุณากรอกรหัสไปรษณีย์");
        Required(issues, "GroupNo", command.GroupNo, "กรุณากรอกกลุ่ม");

        if (command.Beneficiaries.Count == 0)
        {
            issues.Add(new("Beneficiary1.FirstName", "กรุณากรอกผู้รับเงินสงเคราะห์อย่างน้อย 1 คน"));
        }

        foreach (var beneficiary in command.Beneficiaries.OrderBy(value => value.SlotNo))
        {
            var prefix = $"Beneficiary{beneficiary.SlotNo}";
            Required(issues, $"{prefix}.Title", beneficiary.Title, "กรุณากรอกคำนำหน้าผู้รับเงินสงเคราะห์");
            Required(issues, $"{prefix}.FirstName", beneficiary.FirstName, "กรุณากรอกชื่อผู้รับเงินสงเคราะห์");
            Required(issues, $"{prefix}.LastName", beneficiary.LastName, "กรุณากรอกนามสกุลผู้รับเงินสงเคราะห์");
            Required(issues, $"{prefix}.Relationship", beneficiary.Relationship, "กรุณากรอกความสัมพันธ์");
            Required(issues, $"{prefix}.PersonalIdCard", beneficiary.PersonalIdCard, "กรุณากรอกเลขประจำตัวประชาชนผู้รับเงินสงเคราะห์");
            if (!string.IsNullOrWhiteSpace(beneficiary.PersonalIdCard) &&
                (beneficiary.PersonalIdCard.Length != 13 || beneficiary.PersonalIdCard.Any(character => !char.IsAsciiDigit(character))))
            {
                issues.Add(new($"{prefix}.PersonalIdCard", "เลขประจำตัวประชาชนผู้รับเงินสงเคราะห์ต้องมี 13 หลัก"));
            }
            Required(issues, $"{prefix}.Mobile", beneficiary.Mobile, "กรุณากรอกโทรศัพท์ผู้รับเงินสงเคราะห์");
            Required(issues, $"{prefix}.HouseNo", beneficiary.HouseNo, "กรุณากรอกบ้านเลขที่ผู้รับเงินสงเคราะห์");
            Required(issues, $"{prefix}.Under", beneficiary.Under, "กรุณากรอกสังกัด/ใต้ของผู้รับเงินสงเคราะห์");
            Required(issues, $"{prefix}.Moo", beneficiary.Moo, "กรุณากรอกหมู่ของผู้รับเงินสงเคราะห์");
            Required(issues, $"{prefix}.Subdistrict", beneficiary.Subdistrict, "กรุณากรอกตำบลผู้รับเงินสงเคราะห์");
            Required(issues, $"{prefix}.District", beneficiary.District, "กรุณากรอกอำเภอผู้รับเงินสงเคราะห์");
            Required(issues, $"{prefix}.Province", beneficiary.Province, "กรุณากรอกจังหวัดผู้รับเงินสงเคราะห์");
            Required(issues, $"{prefix}.PostalCode", beneficiary.PostalCode, "กรุณากรอกรหัสไปรษณีย์ผู้รับเงินสงเคราะห์");
        }

        return issues;
    }

    public static IReadOnlyList<RegistrationValidationIssue> Validate(UpdateMemberCommand command) => Validate(new RegisterMemberCommand(
        command.Title, command.FirstName, command.LastName, command.Gender, command.PersonalIdCard, command.BirthDate,
        command.HouseNo, command.Under, command.Moo, command.Subdistrict, command.PostalCode, command.Mobile, command.GroupNo,
        command.ApplicationDate, command.ApprovalDate, command.Beneficiaries, command.District, command.Province));

    private static void Required(List<RegistrationValidationIssue> issues, string field, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new(field, message));
        }
    }
}
