using System.Text;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Coordinators;

namespace ChapanakitCare.Web.Pages.Members;

public static class MemberRegistryCsv
{
    public static readonly IReadOnlyDictionary<string, string> Headers = new Dictionary<string, string>
    {
        ["runNo"] = "เลขทะเบียน", ["name"] = "ชื่อ–นามสกุล", ["role"] = "ตำแหน่ง",
        ["personalIdCard"] = "เลขประชาชน", ["gender"] = "เพศ", ["birthDate"] = "วันเกิด",
        ["age"] = "อายุ", ["groupNo"] = "กลุ่มสมาชิก", ["address"] = "ที่อยู่",
        ["applicationDate"] = "วันที่สมัคร", ["approvalDate"] = "วันอนุมัติ",
        ["coverageDate"] = "วันคุ้มครอง", ["beneficiary1"] = "ผู้รับเงิน 1",
        ["relationship1"] = "ความสัมพันธ์ 1", ["beneficiary2"] = "ผู้รับเงิน 2",
        ["relationship2"] = "ความสัมพันธ์ 2", ["advanceUnits"] = "เงินล่วงหน้า (คน)",
        ["status"] = "สถานะ"
    };

    public static string Create(IReadOnlyList<Member> members,
        IReadOnlyDictionary<Guid, IReadOnlyList<MemberBeneficiary>> beneficiariesByMember,
        IReadOnlyList<string> selectedColumns, IReadOnlyList<string> visibleComponents,
        IReadOnlyDictionary<Guid, CoordinatorRoleView>? roles = null)
    {
        var columns = selectedColumns.Where(Headers.ContainsKey).Distinct().ToArray();
        var components = visibleComponents.ToHashSet(StringComparer.Ordinal);
        var csv = new StringBuilder("\uFEFF");
        AppendRow(csv, columns.Select(column => Headers[column]));
        foreach (var member in members)
        {
            var beneficiaries = beneficiariesByMember.GetValueOrDefault(member.Id) ?? [];
            var first = beneficiaries.FirstOrDefault(value => value.SlotNo == 1);
            var second = beneficiaries.FirstOrDefault(value => value.SlotNo == 2);
            AppendRow(csv, columns.Select(column => Value(column, member, first, second, components, roles)));
        }
        return csv.ToString();
    }

    private static string Value(string column, Member member, MemberBeneficiary? first, MemberBeneficiary? second,
        HashSet<string> components, IReadOnlyDictionary<Guid, CoordinatorRoleView>? roles) => column switch
    {
        "runNo" => member.RunNo,
        "name" => Join(
            (components.Contains("name.title") ? member.Title : null) + (components.Contains("name.firstName") ? member.FirstName : null),
            components.Contains("name.lastName") ? member.LastName : null),
        "role" => roles?.GetValueOrDefault(member.Id)?.RoleLabel ?? "–",
        "personalIdCard" => member.PersonalIdCard ?? "–",
        "gender" => member.Gender ?? "–",
        "birthDate" => member.BirthDate is { } birth ? IndexModel.ThaiDate(birth) : "–",
        "age" => IndexModel.Age(member)?.ToString() ?? "–",
        "groupNo" => member.GroupNo ?? "–",
        "address" => Join(
            components.Contains("address.houseNo") ? member.HouseNo : null,
            components.Contains("address.moo") && !string.IsNullOrWhiteSpace(member.Moo) ? $"ม.{member.Moo}" : null,
            components.Contains("address.under") ? member.Under : null,
            components.Contains("address.subdistrict") ? $"ต.{member.Subdistrict}" : null,
            components.Contains("address.district") ? $"อ.{member.District}" : null,
            components.Contains("address.province") ? $"จ.{member.Province}" : null,
            components.Contains("address.postalCode") ? member.PostalCode : null),
        "applicationDate" => IndexModel.ThaiDate(member.ApplicationDate),
        "approvalDate" => IndexModel.ThaiDate(member.ApprovalDate),
        "coverageDate" => IndexModel.ThaiDate(member.CoverageStartDate),
        "beneficiary1" => BeneficiaryName(first),
        "relationship1" => first?.Relationship ?? "–",
        "beneficiary2" => BeneficiaryName(second),
        "relationship2" => second?.Relationship ?? "–",
        "advanceUnits" => member.AdvanceUnitsBalance.ToString(),
        "status" => member.Status switch { MemberStatus.Normal => "ปกติ", MemberStatus.Deceased => "เสียชีวิต", _ => "ลาออก" },
        _ => ""
    };

    private static string BeneficiaryName(MemberBeneficiary? beneficiary) => beneficiary is null
        ? "–" : Join($"{beneficiary.Title}{beneficiary.FirstName}", beneficiary.LastName);

    private static string Join(params string?[] parts)
    {
        var result = string.Join(" ", parts.Where(value => !string.IsNullOrWhiteSpace(value)));
        return result.Length == 0 ? "–" : result;
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string> values) =>
        csv.Append(string.Join(",", values.Select(Escape))).Append("\r\n");

    private static string Escape(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
