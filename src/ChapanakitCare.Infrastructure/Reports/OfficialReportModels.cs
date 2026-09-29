using ChapanakitCare.Domain;

namespace ChapanakitCare.Infrastructure.Reports;

public sealed record MemberByManagerReportSource(
    string RunNo, string? Title, string FirstName, string LastName, string? PersonalIdCard,
    DateOnly ApplicationDate, DateOnly ApprovalDate, DateOnly CoverageStartDate, DateOnly? BirthDate,
    string Address, IReadOnlyList<string> Beneficiaries, IReadOnlyList<string>? BeneficiaryRelationships = null);

public sealed record MemberByManagerReportRow(
    string SequenceNo, string RunNo, string MemberName, string PersonalIdCard, string ApplicationDate,
    string ApprovalDate, string CoverageStartDate, string BirthDate, string Address, string BeneficiaryName,
    string BeneficiaryRelationship = "-");

public sealed record ManagerGroupOption(string GroupNo, string? LeaderName)
{
    public string DisplayLabel => string.IsNullOrWhiteSpace(LeaderName)
        ? $"{GroupNo} — ยังไม่มีหัวหน้ากลุ่ม"
        : $"{GroupNo} — {LeaderName}";
}

public static class MemberByManagerReportRows
{
    public static IReadOnlyList<MemberByManagerReportRow> Expand(MemberByManagerReportSource source, int sequenceNo = 1)
    {
        var beneficiaries = source.Beneficiaries.Count == 0 ? ["-"] : source.Beneficiaries;
        return beneficiaries.Select((beneficiary, index) => new MemberByManagerReportRow(
            index == 0 ? sequenceNo.ToString() : string.Empty, index == 0 ? source.RunNo : string.Empty,
            index == 0 ? $"{source.Title}{source.FirstName} {source.LastName}".Trim() : string.Empty,
            index == 0 ? source.PersonalIdCard ?? "-" : string.Empty,
            index == 0 ? ThaiReportFormat.Date(source.ApplicationDate) : string.Empty,
            index == 0 ? ThaiReportFormat.Date(source.ApprovalDate) : string.Empty,
            index == 0 ? ThaiReportFormat.Date(source.CoverageStartDate) : string.Empty,
            index == 0 ? ThaiReportFormat.Date(source.BirthDate) : string.Empty,
            index == 0 ? source.Address : string.Empty, beneficiary,
            Relationship(source.BeneficiaryRelationships, index, source.Beneficiaries.Count == 0))).ToList();
    }

    private static string Relationship(IReadOnlyList<string>? relationships, int index, bool hasNoBeneficiaries) =>
        hasNoBeneficiaries || relationships is null || index >= relationships.Count || string.IsNullOrWhiteSpace(relationships[index])
            ? "-"
            : relationships[index];
}

public sealed record MonthlyMemberReportRow(string SequenceNo, string MemberName, string GroupNo, string RunNo, string ApplicationDate, string ApprovalDate, string BirthDate, string Age, string PersonalIdCard, string Address, string Mobile, string BeneficiaryName);
public sealed record SakOneReportRow(string SequenceNo, string MemberName, string RunNo, string ApplicationDate, string MemberType, string BirthDate, string Address, string Beneficiary, string FuneralManager, string ChangedDate, string ChangedFrom, string ChangedTo, string EndDate, string EndReason, string Notes, string Spouse = "-");

public static class ThaiReportFormat
{
    public static string Date(DateOnly? value) => value is null ? "-" : ThaiBuddhistDate.Format(value.Value);
    public static string Month(DateOnly value) => $"{value.Month:00}/{value.Year + 543:0000}";
    public static string Age(DateOnly? birthDate, DateOnly asOf)
    {
        if (birthDate is null) return "-";
        var years = asOf.Year - birthDate.Value.Year;
        if (birthDate.Value.AddYears(years) > asOf) years--;
        return years.ToString();
    }
}

internal static class OfficialReportText
{
    public const string Organization = "สมาคมฌาปนกิจสงเคราะห์";
}
