using ChapanakitCare.Domain;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChapanakitCare.Infrastructure.AccountingOperations;

public sealed class DeathAccountingService(AppDbContext database)
{
    public async Task<long?> GetAdvanceAsync(Guid memberId, CancellationToken ct = default)
    {
        var book = await database.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(x => x.Code == AccountingBookCode.Welfare, ct);
        if (book is null || !book.IsActivated) return null;
        var amounts = await MemberAmountsAsync(book.Id, memberId, ct);
        return checked(amounts.Advance - amounts.Debt);
    }

    public async Task AccrueAsync(DeathCase death, DeathBenefitResult calculation, IReadOnlyList<MemberBeneficiary> beneficiaries,
        int feeBasisPoints, string actor, CancellationToken ct = default)
    {
        var welfare = await database.AccountingBooks.SingleOrDefaultAsync(x => x.Code == AccountingBookCode.Welfare, ct);
        if (welfare is null || !welfare.IsActivated || calculation.Eligibility != DeathEligibility.Payable) return;
        if (database.Database.CurrentTransaction is null)
            throw new AccountingValidationException("การตั้งหนี้เงินสงเคราะห์ต้องอยู่ในรายการยืนยันการเสียชีวิตเดียวกัน");
        if (feeBasisPoints != 400 || calculation.TotalBenefitSatang < 0)
            throw new AccountingValidationException("บัญชีใช้อัตราค่าหักร้อยละ 4 และยอดจ่ายรวมต้องไม่ติดลบ");
        var association = await database.AccountingBooks.SingleOrDefaultAsync(x => x.Code == AccountingBookCode.Association, ct);
        if (association is null || !association.IsActivated)
            throw new AccountingValidationException("กรุณาเปิดสมุดบัญชีสมาคมก่อนยืนยันเงินสงเคราะห์");
        var contributors = await database.Members.Where(x => x.Id != death.MemberId && x.Status == MemberStatus.Normal
            && x.ArchivedAtUtc == null).ToListAsync(ct);
        if (contributors.Count != calculation.ContributorCount)
            throw new AccountingValidationException("จำนวนสมาชิกเปลี่ยนระหว่างคำนวณ กรุณาคำนวณใหม่");
        var lines = new List<AccountingPostLine>();
        var rate = calculation.ContributorCount == 0 ? 0 : calculation.GrossCollectionSatang / calculation.ContributorCount;
        foreach (var member in contributors)
        {
            var amounts = await MemberAmountsAsync(welfare.Id, member.Id, ct);
            var funded = Math.Min(amounts.Advance, rate);
            var dimensions = new AccountingDimensions(MemberId: member.Id, DeathCaseId: death.Id,
                PartySnapshot: $"{member.RunNo} {member.Title}{member.FirstName} {member.LastName}");
            if (funded > 0) lines.Add(new("2000", funded, 0, dimensions));
            if (rate > funded) lines.Add(new("1200", rate - funded, 0, dimensions));
        }
        var deceased = await MemberAmountsAsync(welfare.Id, death.MemberId, ct);
        var deceasedDimensions = new AccountingDimensions(MemberId: death.MemberId, DeathCaseId: death.Id);
        if (deceased.Advance > 0) lines.Add(new("2000", deceased.Advance, 0, deceasedDimensions));
        if (deceased.Debt > 0) lines.Add(new("1200", 0, deceased.Debt, deceasedDimensions));
        for (var index = 0; index < beneficiaries.Count; index++)
        {
            var beneficiary = beneficiaries[index];
            var share = calculation.BeneficiarySharesSatang[index];
            if (share > 0) lines.Add(new("2100", 0, share, new(DeathCaseId: death.Id, BeneficiarySlotNo: beneficiary.SlotNo,
                PartySnapshot: $"{beneficiary.Title}{beneficiary.FirstName} {beneficiary.LastName}")));
        }
        var token = $"death-accrual-{death.Id:N}";
        var snapshot = JsonSerializer.Serialize(new { death.Id, death.DeathCaseNo, Calculation = calculation });
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
        var author = new AccountingActor("local-user", actor, Environment.MachineName, "accounting");
        var source = new AccountingSourceReference("death_accrual", death.Id.ToString(), snapshot);
        var posting = new AccountingPostingService(database);
        if (calculation.ServiceFeeSatang > 0)
        {
            lines.Add(new("2200", 0, calculation.ServiceFeeSatang));
            await posting.PostLinkedAsync(new(Guid.NewGuid(),
                new(AccountingBookCode.Welfare, "death_accrual", death.DeathCaseNo, death.RecordedBusinessDate, token, fingerprint, author, lines, source),
                new(AccountingBookCode.Association, "death_accrual", death.DeathCaseNo, death.RecordedBusinessDate, token, fingerprint, author,
                    [new("1300", calculation.ServiceFeeSatang, 0), new("4000", 0, calculation.ServiceFeeSatang)], source)), ct);
        }
        else if (lines.Count > 0)
            await posting.PostAsync(new(AccountingBookCode.Welfare, "death_accrual", death.DeathCaseNo,
                death.RecordedBusinessDate, token, fingerprint, author, lines, source), ct);
    }

    private async Task<(long Advance, long Debt)> MemberAmountsAsync(Guid bookId, Guid memberId, CancellationToken ct)
    {
        var lines = await (from line in database.AccountingJournalLines
            join account in database.AccountingAccounts on line.AccountId equals account.Id
            where line.BookId == bookId && line.MemberId == memberId && (account.Code == "2000" || account.Code == "1200")
            select new { account.Code, line.DebitSatang, line.CreditSatang }).ToListAsync(ct);
        var advance = lines.Where(x => x.Code == "2000").Aggregate(0L, (sum, x) => checked(sum + x.CreditSatang - x.DebitSatang));
        var debt = lines.Where(x => x.Code == "1200").Aggregate(0L, (sum, x) => checked(sum + x.DebitSatang - x.CreditSatang));
        if (advance < 0 || debt < 0) throw new AccountingValidationException("ยอดเงินสมาชิกผิดด้าน กรุณาตรวจสอบบัญชี");
        return (advance, debt);
    }
}
