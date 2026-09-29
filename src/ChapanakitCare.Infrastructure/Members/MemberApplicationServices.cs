using System.Globalization;
using System.Text.Json;
using ChapanakitCare.Domain;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Coordinators;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Infrastructure.Members;

public sealed record BeneficiaryCommand(
    int SlotNo,
    string? Title,
    string FirstName,
    string LastName,
    string? Relationship,
    string? PersonalIdCard,
    string? Mobile,
    string? HouseNo,
    string? Under,
    string? Moo,
    string? Subdistrict,
    string? District,
    string? Province,
    string? PostalCode);

public sealed record RegisterMemberCommand(
    string? Title,
    string FirstName,
    string LastName,
    string? Gender,
    string? PersonalIdCard,
    DateOnly? BirthDate,
    string? HouseNo,
    string? Under,
    string? Moo,
    string? Subdistrict,
    string? PostalCode,
    string? Mobile,
    string? GroupNo,
    DateOnly ApplicationDate,
    DateOnly ApprovalDate,
    IReadOnlyList<BeneficiaryCommand> Beneficiaries,
    string? District = null,
    string? Province = null);

public sealed record UpdateMemberCommand(
    int ExpectedVersion,
    string? Title,
    string FirstName,
    string LastName,
    string? Gender,
    string? PersonalIdCard,
    DateOnly? BirthDate,
    string? HouseNo,
    string? Under,
    string? Moo,
    string? Subdistrict,
    string? PostalCode,
    string? Mobile,
    string? GroupNo,
    DateOnly ApplicationDate,
    DateOnly ApprovalDate,
    IReadOnlyList<BeneficiaryCommand> Beneficiaries,
    string? District = null,
    string? Province = null);

public enum MemberDateField
{
    Application,
    Approval,
    Coverage
}

public sealed record MemberSearchQuery(
    string? Text,
    MemberDateField? DateField,
    DateOnly? From,
    DateOnly? To,
    MemberStatus? Status,
    string? Subdistrict = null,
    string? District = null,
    string? Moo = null,
    string? GroupNo = null,
    string? Under = null);

public sealed record MemberSearchPage(IReadOnlyList<Member> Items, int TotalCount);

public sealed class MemberValidationException(string message) : Exception(message);

public sealed class MemberApplicationService(AppDbContext database)
{
    public Task<Member> RegisterAsync(
        RegisterMemberCommand command,
        DateOnly businessDate,
        DateTimeOffset now,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (!RegistrationAgePolicy.IsEligible(command.BirthDate, command.ApplicationDate))
            throw new MemberValidationException(RegistrationAgePolicy.Message);
        ValidateDateOrder(command.ApplicationDate, command.ApprovalDate, businessDate);
        return RegisterCoreAsync(command, businessDate, now, actor, cancellationToken);
    }

    // Historical demo evidence predates today's admission rule.
    internal Task<Member> ImportHistoricalAsync(RegisterMemberCommand command, DateOnly businessDate,
        DateTimeOffset now, string actor, CancellationToken ct) => RegisterCoreAsync(command, businessDate, now, actor, ct);

    private async Task<Member> RegisterCoreAsync(RegisterMemberCommand command, DateOnly businessDate,
        DateTimeOffset now, string actor, CancellationToken cancellationToken)
    {
        Validate(command.FirstName, command.LastName, command.BirthDate, command.ApplicationDate, command.PostalCode, command.Beneficiaries);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var settings = await database.SystemSettings.AsNoTracking().SingleAsync(value => value.Id == 1, cancellationToken);
        var accountingActive = await database.AccountingBooks.AnyAsync(value => value.Code == AccountingBookCode.Welfare && value.IsActivated, cancellationToken);
        var sequence = await database.NumberSequences.SingleAsync(
            value => value.SequenceKey == "member_run_no",
            cancellationToken);
        var memberId = Guid.NewGuid();
        var runNo = $"{sequence.Prefix}{sequence.NextValue.ToString($"D{sequence.Width}", CultureInfo.InvariantCulture)}";
        sequence.NextValue++;
        sequence.UpdatedAtUtc = now;

        var member = new Member
        {
            Id = memberId,
            RunNo = runNo,
            Title = Clean(command.Title),
            FirstName = command.FirstName.Trim(),
            LastName = command.LastName.Trim(),
            Gender = Clean(command.Gender),
            PersonalIdCard = Clean(command.PersonalIdCard),
            BirthDate = command.BirthDate,
            HouseNo = Clean(command.HouseNo),
            Under = Clean(command.Under),
            Moo = Clean(command.Moo),
            Subdistrict = Clean(command.Subdistrict),
            District = Clean(command.District) ?? "ร้องกวาง",
            Province = Clean(command.Province) ?? "แพร่",
            PostalCode = Clean(command.PostalCode),
            Mobile = Clean(command.Mobile),
            GroupNo = Clean(command.GroupNo),
            ApplicationDate = command.ApplicationDate,
            ApprovalDate = command.ApprovalDate,
            CoverageStartDate = CoveragePolicy.CalculateStart(command.ApprovalDate, settings.CoverageWaitDays),
            Status = MemberStatus.Normal,
            AdvanceUnitsBalance = accountingActive ? 0 : settings.ResetTargetUnits,
            Version = 1,
            CreatedAtUtc = now,
            CreatedBy = actor,
            UpdatedAtUtc = now,
            UpdatedBy = actor
        };
        database.Members.Add(member);
        AddBeneficiaries(memberId, command.Beneficiaries, now, actor);
        database.MemberStatusEvents.Add(new MemberStatusEvent
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            ToStatus = "normal",
            EffectiveDate = businessDate,
            SourceType = "registration",
            CreatedAtUtc = now,
            CreatedBy = actor
        });
        database.AdvanceLedgerEntries.Add(new AdvanceLedgerEntry
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            EntryOrder = 1,
            EntryType = accountingActive ? "correction" : "opening_30",
            BusinessDate = businessDate,
            UnitsDelta = member.AdvanceUnitsBalance,
            BalanceBefore = 0,
            BalanceAfter = member.AdvanceUnitsBalance,
            Reason = accountingActive ? "เริ่มสมาชิกโดยยังไม่มีรายการรับเงินจริง" : $"สมาชิกผ่านการอนุมัติและชำระเงินสงเคราะห์ล่วงหน้า {settings.ResetTargetUnits} คนแล้ว",
            CreatedAtUtc = now,
            CreatedBy = actor
        });
        var createdAudit = NewAudit("member.created", memberId, memberId.ToString(), now, actor);
        createdAudit.Reason = $"{member.RunNo} {member.FirstName} {member.LastName}";
        database.AuditEvents.Add(createdAudit);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return member;
    }

    public async Task<Member> UpdateAsync(
        Guid memberId,
        UpdateMemberCommand command,
        DateTimeOffset now,
        string actor,
        CancellationToken cancellationToken = default)
    {
        Validate(command.FirstName, command.LastName, command.BirthDate, command.ApplicationDate, command.PostalCode, command.Beneficiaries);
        ValidateDateOrder(command.ApplicationDate, command.ApprovalDate, DateOnly.FromDateTime(now.LocalDateTime));
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var settings = await database.SystemSettings.AsNoTracking().SingleAsync(value => value.Id == 1, cancellationToken);
        var member = await database.Members.SingleAsync(value => value.Id == memberId, cancellationToken);
        if (member.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException("ข้อมูลสมาชิกถูกแก้ไขจากหน้าจออื่น กรุณาโหลดใหม่");
        }

        await CoordinatorApplicationService.EnsureGroupChangeAllowedAsync(database, member, Clean(command.GroupNo), cancellationToken);

        var operationId = Guid.NewGuid();
        var audit = NewAudit("member.updated", memberId, memberId.ToString(), now, actor, operationId);
        audit.Reason = $"{member.RunNo} {member.FirstName} {member.LastName}";
        database.AuditEvents.Add(audit);
        TrackChange(audit.Id, nameof(Member.Title), member.Title, Clean(command.Title));
        TrackChange(audit.Id, nameof(Member.FirstName), member.FirstName, command.FirstName.Trim());
        TrackChange(audit.Id, nameof(Member.LastName), member.LastName, command.LastName.Trim());
        TrackChange(audit.Id, nameof(Member.Gender), member.Gender, Clean(command.Gender));
        TrackChange(audit.Id, nameof(Member.PersonalIdCard), member.PersonalIdCard, Clean(command.PersonalIdCard), true);
        TrackChange(audit.Id, nameof(Member.BirthDate), member.BirthDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), command.BirthDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        TrackChange(audit.Id, nameof(Member.HouseNo), member.HouseNo, Clean(command.HouseNo));
        TrackChange(audit.Id, nameof(Member.Under), member.Under, Clean(command.Under));
        TrackChange(audit.Id, nameof(Member.Moo), member.Moo, Clean(command.Moo));
        TrackChange(audit.Id, nameof(Member.Subdistrict), member.Subdistrict, Clean(command.Subdistrict));
        TrackChange(audit.Id, nameof(Member.District), member.District, Clean(command.District) ?? member.District);
        TrackChange(audit.Id, nameof(Member.Province), member.Province, Clean(command.Province) ?? member.Province);
        TrackChange(audit.Id, nameof(Member.PostalCode), member.PostalCode, Clean(command.PostalCode));
        TrackChange(audit.Id, nameof(Member.Mobile), member.Mobile, Clean(command.Mobile), true);
        TrackChange(audit.Id, nameof(Member.GroupNo), member.GroupNo, Clean(command.GroupNo));
        TrackChange(audit.Id, nameof(Member.ApplicationDate), member.ApplicationDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), command.ApplicationDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        TrackChange(audit.Id, nameof(Member.ApprovalDate), member.ApprovalDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), command.ApprovalDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        member.Title = Clean(command.Title);
        member.FirstName = command.FirstName.Trim();
        member.LastName = command.LastName.Trim();
        member.Gender = Clean(command.Gender);
        member.PersonalIdCard = Clean(command.PersonalIdCard);
        member.BirthDate = command.BirthDate;
        member.HouseNo = Clean(command.HouseNo);
        member.Under = Clean(command.Under);
        member.Moo = Clean(command.Moo);
        member.Subdistrict = Clean(command.Subdistrict);
        member.District = Clean(command.District) ?? member.District;
        member.Province = Clean(command.Province) ?? member.Province;
        member.PostalCode = Clean(command.PostalCode);
        member.Mobile = Clean(command.Mobile);
        member.GroupNo = Clean(command.GroupNo);
        member.ApplicationDate = command.ApplicationDate;
        member.ApprovalDate = command.ApprovalDate;
        member.CoverageStartDate = CoveragePolicy.CalculateStart(command.ApprovalDate, settings.CoverageWaitDays);
        member.Version++;
        member.UpdatedAtUtc = now;
        member.UpdatedBy = actor;

        var existingBeneficiaries = await database.MemberBeneficiaries
            .Where(value => value.MemberId == memberId && value.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var slot in new[] { 1, 2 })
        {
            var old = existingBeneficiaries.SingleOrDefault(b => b.SlotNo == slot);
            var next = command.Beneficiaries.SingleOrDefault(b => b.SlotNo == slot);
            var oldValues = old is null ? new Dictionary<string, string?>() : BeneficiaryValues(new BeneficiaryCommand(
                old.SlotNo, old.Title, old.FirstName, old.LastName, old.Relationship, old.PersonalIdCard, old.Mobile,
                old.HouseNo, old.Under, old.Moo, old.Subdistrict, old.District, old.Province, old.PostalCode));
            var nextValues = next is null ? new Dictionary<string, string?>() : BeneficiaryValues(next);
            foreach (var field in oldValues.Keys.Union(nextValues.Keys))
                TrackChange(audit.Id, $"Beneficiary{slot}.{field}", oldValues.GetValueOrDefault(field),
                    nextValues.GetValueOrDefault(field), field is "PersonalIdCard" or "Mobile");
        }
        foreach (var existing in existingBeneficiaries)
        {
            existing.IsActive = false;
            existing.ArchivedAtUtc = now;
            existing.ArchivedBy = actor;
            existing.ArchiveReason = "แทนที่ข้อมูลผู้รับเงินสงเคราะห์ในการแก้ไขสมาชิก";
            existing.UpdatedAtUtc = now;
            existing.UpdatedBy = actor;
            existing.Version++;
        }

        AddBeneficiaries(memberId, command.Beneficiaries, now, actor);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return member;
    }

    public async Task<IReadOnlyList<Member>> SearchAsync(
        MemberSearchQuery search,
        CancellationToken cancellationToken = default) =>
        await BuildSearchQuery(search).OrderBy(value => value.RunNo).ToListAsync(cancellationToken);

    public async Task<MemberSearchPage> SearchPageAsync(MemberSearchQuery search, int pageNumber, int pageSize,
        IReadOnlyCollection<Guid>? allowedMemberIds = null, IReadOnlyCollection<Guid>? excludedMemberIds = null,
        string? sortColumn = null, string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1 || pageSize < 1 || pageSize > 200) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var query = BuildSearchQuery(search);
        if (allowedMemberIds is not null) query = query.Where(value => allowedMemberIds.Contains(value.Id));
        if (excludedMemberIds is not null) query = query.Where(value => !excludedMemberIds.Contains(value.Id));
        var total = await query.CountAsync(cancellationToken);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        pageNumber = Math.Min(pageNumber, lastPage);
        var items = await OrderSearch(query, sortColumn, sortDirection)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new MemberSearchPage(items, total);
    }

    private IOrderedQueryable<Member> OrderSearch(IQueryable<Member> query, string? column, string? direction)
    {
        var descending = direction == "desc";
        var ordered = (column, descending) switch
        {
            ("name", false) => query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName),
            ("name", true) => query.OrderByDescending(x => x.FirstName).ThenByDescending(x => x.LastName),
            ("role", false) => query.OrderBy(x => database.CoordinatorPositions.Where(p => p.MemberId == x.Id).Select(p => p.RoleCode).FirstOrDefault()),
            ("role", true) => query.OrderByDescending(x => database.CoordinatorPositions.Where(p => p.MemberId == x.Id).Select(p => p.RoleCode).FirstOrDefault()),
            ("personalIdCard", false) => query.OrderBy(x => x.PersonalIdCard),
            ("personalIdCard", true) => query.OrderByDescending(x => x.PersonalIdCard),
            ("gender", false) => query.OrderBy(x => x.Gender),
            ("gender", true) => query.OrderByDescending(x => x.Gender),
            ("birthDate", false) => query.OrderBy(x => x.BirthDate),
            ("birthDate", true) => query.OrderByDescending(x => x.BirthDate),
            ("age", false) => query.OrderByDescending(x => x.BirthDate),
            ("age", true) => query.OrderBy(x => x.BirthDate),
            ("groupNo", false) => query.OrderBy(x => x.GroupNo),
            ("groupNo", true) => query.OrderByDescending(x => x.GroupNo),
            ("address", false) => query.OrderBy(x => x.Subdistrict).ThenBy(x => x.Moo).ThenBy(x => x.HouseNo),
            ("address", true) => query.OrderByDescending(x => x.Subdistrict).ThenByDescending(x => x.Moo).ThenByDescending(x => x.HouseNo),
            ("applicationDate", false) => query.OrderBy(x => x.ApplicationDate),
            ("applicationDate", true) => query.OrderByDescending(x => x.ApplicationDate),
            ("approvalDate", false) => query.OrderBy(x => x.ApprovalDate),
            ("approvalDate", true) => query.OrderByDescending(x => x.ApprovalDate),
            ("coverageDate", false) => query.OrderBy(x => x.CoverageStartDate),
            ("coverageDate", true) => query.OrderByDescending(x => x.CoverageStartDate),
            ("beneficiary1", false) => query.OrderBy(x => database.MemberBeneficiaries.Where(b => b.MemberId == x.Id && b.IsActive && b.SlotNo == 1).Select(b => b.FirstName).FirstOrDefault()),
            ("beneficiary1", true) => query.OrderByDescending(x => database.MemberBeneficiaries.Where(b => b.MemberId == x.Id && b.IsActive && b.SlotNo == 1).Select(b => b.FirstName).FirstOrDefault()),
            ("beneficiary2", false) => query.OrderBy(x => database.MemberBeneficiaries.Where(b => b.MemberId == x.Id && b.IsActive && b.SlotNo == 2).Select(b => b.FirstName).FirstOrDefault()),
            ("beneficiary2", true) => query.OrderByDescending(x => database.MemberBeneficiaries.Where(b => b.MemberId == x.Id && b.IsActive && b.SlotNo == 2).Select(b => b.FirstName).FirstOrDefault()),
            ("relationship1", false) => query.OrderBy(x => database.MemberBeneficiaries.Where(b => b.MemberId == x.Id && b.IsActive && b.SlotNo == 1).Select(b => b.Relationship).FirstOrDefault()),
            ("relationship1", true) => query.OrderByDescending(x => database.MemberBeneficiaries.Where(b => b.MemberId == x.Id && b.IsActive && b.SlotNo == 1).Select(b => b.Relationship).FirstOrDefault()),
            ("relationship2", false) => query.OrderBy(x => database.MemberBeneficiaries.Where(b => b.MemberId == x.Id && b.IsActive && b.SlotNo == 2).Select(b => b.Relationship).FirstOrDefault()),
            ("relationship2", true) => query.OrderByDescending(x => database.MemberBeneficiaries.Where(b => b.MemberId == x.Id && b.IsActive && b.SlotNo == 2).Select(b => b.Relationship).FirstOrDefault()),
            ("advanceUnits", false) => query.OrderBy(x => x.AdvanceUnitsBalance),
            ("advanceUnits", true) => query.OrderByDescending(x => x.AdvanceUnitsBalance),
            ("status", false) => query.OrderBy(x => x.Status),
            ("status", true) => query.OrderByDescending(x => x.Status),
            (_, true) => query.OrderByDescending(x => x.RunNo),
            _ => query.OrderBy(x => x.RunNo)
        };
        return ordered.ThenBy(x => x.RunNo);
    }

    private IQueryable<Member> BuildSearchQuery(MemberSearchQuery search)
    {
        var query = database.Members.AsNoTracking().Where(value => value.ArchivedAtUtc == null);
        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var text = search.Text.Trim();
            query = query.Where(value =>
                value.RunNo.Contains(text) ||
                value.FirstName.Contains(text) ||
                value.LastName.Contains(text) ||
                (value.Title != null && value.Title.Contains(text)) ||
                (value.Gender != null && value.Gender.Contains(text)) ||
                (value.PersonalIdCard != null && value.PersonalIdCard.Contains(text)) ||
                (value.Mobile != null && value.Mobile.Contains(text)) ||
                (value.HouseNo != null && value.HouseNo.Contains(text)) ||
                (value.Moo != null && value.Moo.Contains(text)) ||
                (value.Under != null && value.Under.Contains(text)) ||
                (value.Subdistrict != null && value.Subdistrict.Contains(text)) ||
                value.District.Contains(text) ||
                value.Province.Contains(text) ||
                (value.PostalCode != null && value.PostalCode.Contains(text)) ||
                (value.GroupNo != null && value.GroupNo.Contains(text)) ||
                database.MemberBeneficiaries.Any(beneficiary =>
                    beneficiary.MemberId == value.Id && beneficiary.IsActive &&
                    ((beneficiary.Title != null && beneficiary.Title.Contains(text)) ||
                     beneficiary.FirstName.Contains(text) || beneficiary.LastName.Contains(text) ||
                     (beneficiary.Relationship != null && beneficiary.Relationship.Contains(text)) ||
                     (beneficiary.PersonalIdCard != null && beneficiary.PersonalIdCard.Contains(text)) ||
                     (beneficiary.Mobile != null && beneficiary.Mobile.Contains(text)) ||
                     (beneficiary.HouseNo != null && beneficiary.HouseNo.Contains(text)) ||
                     (beneficiary.Moo != null && beneficiary.Moo.Contains(text)) ||
                     (beneficiary.Under != null && beneficiary.Under.Contains(text)) ||
                     (beneficiary.Subdistrict != null && beneficiary.Subdistrict.Contains(text)) ||
                     (beneficiary.District != null && beneficiary.District.Contains(text)) ||
                     (beneficiary.Province != null && beneficiary.Province.Contains(text)) ||
                     (beneficiary.PostalCode != null && beneficiary.PostalCode.Contains(text)))));
        }

        if (search.Status is not null)
        {
            query = query.Where(value => value.Status == search.Status);
        }

        if (!string.IsNullOrWhiteSpace(search.Subdistrict))
            query = query.Where(value => value.Subdistrict != null && value.Subdistrict.Contains(search.Subdistrict.Trim()));
        if (!string.IsNullOrWhiteSpace(search.District))
            query = query.Where(value => value.District.Contains(search.District.Trim()));
        if (!string.IsNullOrWhiteSpace(search.Moo))
            query = query.Where(value => value.Moo != null && value.Moo == search.Moo.Trim());
        if (!string.IsNullOrWhiteSpace(search.GroupNo))
            query = query.Where(value => value.GroupNo != null && value.GroupNo.Contains(search.GroupNo.Trim()));
        if (!string.IsNullOrWhiteSpace(search.Under))
            query = query.Where(value => value.Under != null && value.Under.Contains(search.Under.Trim()));

        if (search.DateField is not null && search.From is not null)
        {
            query = search.DateField switch
            {
                MemberDateField.Application => query.Where(value => value.ApplicationDate >= search.From),
                MemberDateField.Approval => query.Where(value => value.ApprovalDate >= search.From),
                MemberDateField.Coverage => query.Where(value => value.CoverageStartDate >= search.From),
                _ => query
            };
        }

        if (search.DateField is not null && search.To is not null)
        {
            query = search.DateField switch
            {
                MemberDateField.Application => query.Where(value => value.ApplicationDate <= search.To),
                MemberDateField.Approval => query.Where(value => value.ApprovalDate <= search.To),
                MemberDateField.Coverage => query.Where(value => value.CoverageStartDate <= search.To),
                _ => query
            };
        }

        return query;
    }

    private static void Validate(
        string firstName,
        string lastName,
        DateOnly? birthDate,
        DateOnly applicationDate,
        string? postalCode,
        IReadOnlyList<BeneficiaryCommand> beneficiaries)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            throw new MemberValidationException("กรุณากรอกชื่อและนามสกุล");
        }

        if (postalCode is not null && postalCode.Trim().Length != 5)
        {
            throw new MemberValidationException("รหัสไปรษณีย์ต้องมี 5 หลัก");
        }

        if (birthDate > applicationDate)
        {
            throw new MemberValidationException("วันเกิดต้องไม่อยู่หลังวันที่สมัคร");
        }

        if (beneficiaries.Count > 2 || beneficiaries.Any(value => value.SlotNo is < 1 or > 2) || beneficiaries.Select(value => value.SlotNo).Distinct().Count() != beneficiaries.Count)
        {
            throw new MemberValidationException("ผู้รับเงินสงเคราะห์มีได้สูงสุด 2 คน และช่องต้องไม่ซ้ำกัน");
        }
    }

    private static void ValidateDateOrder(DateOnly application, DateOnly approval, DateOnly businessDate)
    {
        if (application == default || approval < application || approval > businessDate)
            throw new MemberValidationException("วันที่สมัครและวันอนุมัติต้องไม่อยู่ในอนาคต และวันอนุมัติต้องไม่อยู่ก่อนวันที่สมัคร กรุณาตรวจสอบปี พ.ศ.");
    }

    private void AddBeneficiaries(Guid memberId, IReadOnlyList<BeneficiaryCommand> commands, DateTimeOffset now, string actor)
    {
        foreach (var command in commands)
        {
            database.MemberBeneficiaries.Add(new MemberBeneficiary
            {
                Id = Guid.NewGuid(),
                MemberId = memberId,
                SlotNo = command.SlotNo,
                Title = Clean(command.Title),
                FirstName = command.FirstName.Trim(),
                LastName = command.LastName.Trim(),
                Relationship = Clean(command.Relationship),
                PersonalIdCard = Clean(command.PersonalIdCard),
                Mobile = Clean(command.Mobile),
                HouseNo = Clean(command.HouseNo),
                Under = Clean(command.Under),
                Moo = Clean(command.Moo),
                Subdistrict = Clean(command.Subdistrict),
                District = Clean(command.District),
                Province = Clean(command.Province),
                PostalCode = Clean(command.PostalCode),
                IsActive = true,
                Version = 1,
                CreatedAtUtc = now,
                CreatedBy = actor,
                UpdatedAtUtc = now,
                UpdatedBy = actor
            });
        }
    }

    private static Dictionary<string, string?> BeneficiaryValues(BeneficiaryCommand b) => new()
    {
        ["Title"] = Clean(b.Title), ["FirstName"] = Clean(b.FirstName), ["LastName"] = Clean(b.LastName),
        ["Relationship"] = Clean(b.Relationship), ["PersonalIdCard"] = Clean(b.PersonalIdCard), ["Mobile"] = Clean(b.Mobile),
        ["HouseNo"] = Clean(b.HouseNo), ["Under"] = Clean(b.Under), ["Moo"] = Clean(b.Moo),
        ["Subdistrict"] = Clean(b.Subdistrict), ["District"] = Clean(b.District), ["Province"] = Clean(b.Province),
        ["PostalCode"] = Clean(b.PostalCode)
    };

    private void TrackChange(Guid auditId, string field, string? oldValue, string? newValue, bool sensitive = false)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        database.AuditFieldChanges.Add(new AuditFieldChange
        {
            Id = Guid.NewGuid(),
            AuditEventId = auditId,
            FieldName = field,
            OldValueDisplay = oldValue,
            NewValueDisplay = newValue,
            IsSensitive = sensitive
        });
    }

    private static AuditEvent NewAudit(
        string action,
        Guid? memberId,
        string entityId,
        DateTimeOffset now,
        string actor,
        Guid? operationId = null) => new()
        {
            Id = Guid.NewGuid(),
            OperationId = operationId ?? Guid.NewGuid(),
            OccurredAtUtc = now,
            ActorUserId = "local-user",
            ActorDisplayName = actor,
            MachineName = Environment.MachineName,
            Action = action,
            EntityType = memberId is null ? "system_settings" : "member",
            EntityId = entityId,
            MemberId = memberId,
            AppVersion = "checkpoint-2"
        };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SettingsCommand(
    long? RegistrationFeeSatang,
    int ServiceFeeBasisPoints,
    long WelfarePerMemberSatang,
    int ResetTargetUnits,
    int CoverageWaitDays,
    int SpecialNonPayWindowDays,
    int DeathWarningThreshold);

public sealed class SettingsApplicationService(AppDbContext database)
{
    public Task<SystemSettings> GetAsync(CancellationToken cancellationToken = default) =>
        database.SystemSettings.AsNoTracking().SingleAsync(value => value.Id == 1, cancellationToken);

    public async Task<SystemSettings> SaveAsync(
        SettingsCommand command,
        DateTimeOffset now,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (command.RegistrationFeeSatang is < 0 ||
            command.ServiceFeeBasisPoints is < 0 or > 10_000 ||
            command.WelfarePerMemberSatang <= 0 ||
            command.ResetTargetUnits < 0 ||
            command.CoverageWaitDays < 0 ||
            command.SpecialNonPayWindowDays < 0 ||
            command.DeathWarningThreshold <= 0)
        {
            throw new MemberValidationException("ค่าตั้งต้นระบบไม่ถูกต้อง");
        }

        if (command.ServiceFeeBasisPoints != 400 && await database.AccountingBooks.AnyAsync(x => x.IsActivated, cancellationToken))
            throw new MemberValidationException("เมื่อเปิดบัญชีแล้ว รายได้สมาคมใช้อัตราค่าหักเงินสงเคราะห์ร้อยละ 4 เท่านั้น");
        var settings = await database.SystemSettings.SingleAsync(value => value.Id == 1, cancellationToken);
        if (command.WelfarePerMemberSatang != settings.WelfarePerMemberSatang
            && await database.AccountingBooks.AnyAsync(x => x.Code == AccountingBookCode.Welfare && x.IsActivated, cancellationToken))
            throw new MemberValidationException("เมื่อเปิดบัญชีเงินสงเคราะห์แล้ว ยังไม่รองรับการเปลี่ยนอัตราเงินสงเคราะห์ต่อสมาชิก เพราะต้องปรับจำนวนหน่วยตามยอดเงินจริงพร้อมประวัติการแก้ไข");
        settings.SettingsRevision++;
        settings.RegistrationFeeSatang = command.RegistrationFeeSatang;
        settings.ServiceFeeBasisPoints = command.ServiceFeeBasisPoints;
        settings.WelfarePerMemberSatang = command.WelfarePerMemberSatang;
        settings.ResetTargetUnits = command.ResetTargetUnits;
        settings.CoverageWaitDays = command.CoverageWaitDays;
        settings.SpecialNonPayWindowDays = command.SpecialNonPayWindowDays;
        settings.DeathWarningThreshold = command.DeathWarningThreshold;
        settings.UpdatedAtUtc = now;
        settings.UpdatedBy = actor;
        database.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OperationId = Guid.NewGuid(),
            OccurredAtUtc = now,
            ActorUserId = "local-user",
            ActorDisplayName = actor,
            MachineName = Environment.MachineName,
            Action = "settings.updated",
            EntityType = "system_settings",
            EntityId = "1",
            AppVersion = "checkpoint-2"
        });
        await database.SaveChangesAsync(cancellationToken);
        return settings;
    }
}

public sealed record TablePreference(
    IReadOnlyList<string> ColumnOrder,
    IReadOnlyList<string> HiddenColumns,
    IReadOnlyList<string> VisibleComponents,
    string? SortColumn = null,
    string? SortDirection = null);

public static class MemberTableComponents
{
    public static readonly string[] All =
    [
        "name.title", "name.firstName", "name.lastName",
        "address.houseNo", "address.moo", "address.under", "address.subdistrict",
        "address.district", "address.province", "address.postalCode"
    ];
}

public sealed class DemoImportService(AppDbContext database)
{
    public async Task<int> ImportAsync(
        IReadOnlyList<RegisterMemberCommand> records,
        DateOnly businessDate,
        DateTimeOffset now,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var memberService = new MemberApplicationService(database);
        var imported = 0;
        foreach (var record in records)
        {
            var alreadyExists = !string.IsNullOrWhiteSpace(record.PersonalIdCard)
                ? await database.Members.AnyAsync(value => value.PersonalIdCard == record.PersonalIdCard.Trim(), cancellationToken)
                : await database.Members.AnyAsync(value =>
                    value.Title == record.Title &&
                    value.FirstName == record.FirstName.Trim() &&
                    value.LastName == record.LastName.Trim() &&
                    value.BirthDate == record.BirthDate &&
                    value.ApplicationDate == record.ApplicationDate,
                    cancellationToken);
            if (alreadyExists)
            {
                continue;
            }

            await memberService.ImportHistoricalAsync(record, businessDate, now.AddMilliseconds(imported), actor, cancellationToken);
            imported++;
        }

        return imported;
    }
}

public sealed class DemoDataMaintenanceService(AppDbContext database)
{
    public async Task<int> ClearMembersAsync(
        DateTimeOffset now,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (await database.AccountingBooks.AnyAsync(x => x.IsActivated, cancellationToken)
            || await database.AccountingJournals.AnyAsync(cancellationToken)
            || await database.Set<WelfareCollection>().AnyAsync(cancellationToken))
            throw new MemberValidationException("มีบัญชีหรือใบเรียกเก็บแล้ว ไม่สามารถล้างประวัติสมาชิกได้");
        var memberCount = await database.Members.CountAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);


        await database.CoordinatorEvents.ExecuteDeleteAsync(cancellationToken);
        await database.CoordinatorPositions.ExecuteDeleteAsync(cancellationToken);
        await database.Notifications.ExecuteDeleteAsync(cancellationToken);
        await database.AdvanceResetLines.ExecuteDeleteAsync(cancellationToken);
        await database.AdvanceLedgerEntries.ExecuteDeleteAsync(cancellationToken);
        await database.DeathRecipientPhotos.ExecuteDeleteAsync(cancellationToken);
        await database.DeathBeneficiarySnapshots.ExecuteDeleteAsync(cancellationToken);
        await database.DeathCalculations.ExecuteDeleteAsync(cancellationToken);
        await database.DeathMemberSnapshots.ExecuteDeleteAsync(cancellationToken);
        await database.DeathCases.ExecuteDeleteAsync(cancellationToken);
        await database.AdvanceResetBatches.ExecuteDeleteAsync(cancellationToken);
        await database.MemberStatusEvents.ExecuteDeleteAsync(cancellationToken);
        await database.MemberBeneficiaries.ExecuteDeleteAsync(cancellationToken);
        await database.Members.ExecuteDeleteAsync(cancellationToken);

        foreach (var sequence in await database.NumberSequences.ToListAsync(cancellationToken))
        {
            sequence.NextValue = 1;
            sequence.UpdatedAtUtc = now;
        }

        database.AuditEvents.Add(ActivityAudit.Create("demo.members_cleared", "demo_data", "members", now, actor,
            $"ล้างสมาชิก {memberCount} คน พร้อมข้อมูลดำเนินงานสาธิต ประวัติการเปลี่ยนแปลงยังคงอยู่"));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return memberCount;
    }
}

public static class AuditActionLabels
{
    public static string ToThai(string action) => action switch
    {
        "member.created" => "เพิ่มสมาชิก",
        "member.updated" => "แก้ไขข้อมูลสมาชิก",
        "advance.reset" => "รีเซ็ตยอดเงินสงเคราะห์ล่วงหน้า",
        "member.resigned" => "บันทึกสมาชิกลาออก",
        "settings.updated" => "แก้ไขค่าตั้งต้นระบบ",
        "death.confirmed" => "บันทึกการเสียชีวิต",
        "coordinator.appoint" => "แต่งตั้งผู้ประสานงาน",
        "coordinator.replace" => "เปลี่ยนผู้ประสานงาน",
        "coordinator.end" => "สิ้นสุดหน้าที่ผู้ประสานงาน",
        "demo.members_cleared" => "ล้างข้อมูลสมาชิกสาธิต",
        _ => "รายการระบบ"
    };
}

public sealed class TablePreferenceService(AppDbContext database)
{
    public async Task<TablePreference> GetAsync(string profileKey, string tableKey, CancellationToken cancellationToken = default)
    {
        var preference = await database.UiTablePreferences.AsNoTracking().SingleOrDefaultAsync(
            value => value.ProfileKey == profileKey && value.TableKey == tableKey,
            cancellationToken);
        return preference is null
            ? new TablePreference([], [], MemberTableComponents.All)
            : new TablePreference(
                JsonSerializer.Deserialize<string[]>(preference.ColumnOrderJson) ?? [],
                JsonSerializer.Deserialize<string[]>(preference.HiddenColumnsJson) ?? [],
                JsonSerializer.Deserialize<string[]>(preference.VisibleComponentsJson) ?? [],
                DeserializeSort(preference.SortJson).Column,
                DeserializeSort(preference.SortJson).Direction);
    }

    public async Task SaveAsync(
        string profileKey,
        string tableKey,
        IReadOnlyList<string> columnOrder,
        IReadOnlyList<string> hiddenColumns,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
        => await SaveAsync(profileKey, tableKey, columnOrder, hiddenColumns, [], null, null, now, cancellationToken);

    public async Task SaveAsync(
        string profileKey,
        string tableKey,
        IReadOnlyList<string> columnOrder,
        IReadOnlyList<string> hiddenColumns,
        IReadOnlyList<string> visibleComponents,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
        => await SaveAsync(profileKey, tableKey, columnOrder, hiddenColumns, visibleComponents, null, null, now, cancellationToken);

    public async Task SaveAsync(
        string profileKey,
        string tableKey,
        IReadOnlyList<string> columnOrder,
        IReadOnlyList<string> hiddenColumns,
        IReadOnlyList<string> visibleComponents,
        string? sortColumn,
        string? sortDirection,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var preference = await database.UiTablePreferences.SingleOrDefaultAsync(
            value => value.ProfileKey == profileKey && value.TableKey == tableKey,
            cancellationToken);
        if (preference is null)
        {
            preference = new UiTablePreference
            {
                Id = Guid.NewGuid(),
                ProfileKey = profileKey,
                TableKey = tableKey
            };
            database.UiTablePreferences.Add(preference);
        }

        preference.ColumnOrderJson = JsonSerializer.Serialize(columnOrder);
        preference.HiddenColumnsJson = JsonSerializer.Serialize(hiddenColumns);
        preference.VisibleComponentsJson = JsonSerializer.Serialize(visibleComponents);
        var normalizedDirection = sortDirection is "asc" or "desc" ? sortDirection : null;
        preference.SortJson = JsonSerializer.Serialize(new SavedSort(
            normalizedDirection is null ? null : Clean(sortColumn), normalizedDirection));
        preference.UpdatedAtUtc = now;
        await database.SaveChangesAsync(cancellationToken);
    }

    private static SavedSort DeserializeSort(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(null, null);
        try { return JsonSerializer.Deserialize<SavedSort>(json) ?? new(null, null); }
        catch (JsonException) { return new(null, null); }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private sealed record SavedSort(string? Column, string? Direction);

    public async Task ResetAsync(string profileKey, string tableKey, CancellationToken cancellationToken = default)
    {
        var preference = await database.UiTablePreferences.SingleOrDefaultAsync(
            value => value.ProfileKey == profileKey && value.TableKey == tableKey,
            cancellationToken);
        if (preference is not null)
        {
            database.UiTablePreferences.Remove(preference);
            await database.SaveChangesAsync(cancellationToken);
        }
    }
}
