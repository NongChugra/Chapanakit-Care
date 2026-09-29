using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Accounting;
using ChapanakitCare.Infrastructure.AccountingOperations;
using ChapanakitCare.Infrastructure.Deaths;
using ChapanakitCare.Infrastructure.Members;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingWorkflowGuardTests
{
    [Fact]
    public async Task Dashboard_explains_active_rate_change_rejection_without_server_error()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "ศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var settings = await store.Db.SystemSettings.AsNoTracking().SingleAsync();
        var page = new ChapanakitCare.Web.Pages.IndexModel(store.Db,
            new ChapanakitCare.Infrastructure.Coordinators.CoordinatorApplicationService(store.Db),
            new SettingsApplicationService(store.Db), null!, null!, null!, null!);
        page.Input.ServiceFeePercent = 4;
        page.Input.WelfarePerMemberBaht = settings.WelfarePerMemberSatang / 100m + 1;
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await page.OnPostAsync());
        Assert.False(page.ModelState.IsValid);
        Assert.Contains(page.ModelState.Values.SelectMany(x => x.Errors), x => x.ErrorMessage.Contains("อัตราเงินสงเคราะห์"));
    }

    [Fact]
    public async Task Active_welfare_book_rejects_rate_changes_but_allows_other_settings()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "ศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var settings = await store.Db.SystemSettings.AsNoTracking().SingleAsync();
        var command = new SettingsCommand(settings.RegistrationFeeSatang, 400, settings.WelfarePerMemberSatang + 100,
            settings.ResetTargetUnits, settings.CoverageWaitDays, settings.SpecialNonPayWindowDays, settings.DeathWarningThreshold);
        await Assert.ThrowsAsync<MemberValidationException>(() => new SettingsApplicationService(store.Db).SaveAsync(command, FinanceTestStore.Now, "tester"));
        await store.Db.SaveChangesAsync();
        Assert.Equal(settings.WelfarePerMemberSatang, (await store.Db.SystemSettings.AsNoTracking().SingleAsync()).WelfarePerMemberSatang);
        await new SettingsApplicationService(store.Db).SaveAsync(command with
        { WelfarePerMemberSatang = settings.WelfarePerMemberSatang, DeathWarningThreshold = settings.DeathWarningThreshold + 1 }, FinanceTestStore.Now, "tester");
        Assert.Equal(settings.DeathWarningThreshold + 1, (await store.Db.SystemSettings.AsNoTracking().SingleAsync()).DeathWarningThreshold);
    }

    [Fact]
    public async Task Active_accounting_replaces_reset_reminders_with_collection_guidance_without_creating_money()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var notices = new NotificationService(store.Db);
        await notices.RefreshAsync(new(2026, 9, 1), FinanceTestStore.Now);
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "ศูนย์", "open", []), FinanceTestStore.Now, "tester");
        await notices.RefreshAsync(new(2026, 10, 1), FinanceTestStore.Now);
        var active = await store.Db.Notifications.Where(x => x.State == "active").ToListAsync();
        Assert.DoesNotContain(active, x => x.Message.Contains("รีเซ็ต", StringComparison.Ordinal));
        Assert.Contains(active, x => x.Message.Contains("เรียกเก็บ", StringComparison.Ordinal));
        Assert.Empty(await store.Db.AccountingJournals.ToListAsync());
        Assert.Empty(await store.Db.Set<WelfareCollection>().ToListAsync());
    }

    [Fact]
    public async Task Death_form_displays_accounting_setup_error_without_saving_a_death()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await store.AddMemberAsync("00001");
        await store.AddMemberAsync("00002");
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "เริ่มศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var page = new ChapanakitCare.Web.Pages.Deaths.CreateModel(new DeathApplicationService(store.Db))
        { RunNo = "00001", CertificateNo = "DC-1", CertificateDate = FinanceTestStore.Date, ReportedCertificateDate = FinanceTestStore.Date, Cause = "เหตุ" };
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await page.OnPostConfirmAsync());
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(await store.Db.DeathCases.ToListAsync());
    }

    [Fact]
    public async Task Legacy_reset_page_displays_validation_instead_of_throwing_when_accounting_is_active()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var page = new ChapanakitCare.Web.Pages.Advances.IndexModel(store.Db, new AdvanceResetService(store.Db), new NotificationService(store.Db))
        { ResetToken = Guid.NewGuid().ToString("N") };
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await page.OnPostResetAsync());
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(await store.Db.AdvanceResetBatches.ToListAsync());
    }

    [Fact]
    public async Task Active_books_preserve_member_history()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await store.AddMemberAsync("00001");
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open", []), FinanceTestStore.Now, "tester");
        await Assert.ThrowsAsync<MemberValidationException>(() => new DemoDataMaintenanceService(store.Db).ClearMembersAsync(FinanceTestStore.Now, "tester"));
        Assert.Single(await store.Db.Members.ToListAsync());
    }

    [Fact]
    public async Task Active_books_preserve_the_four_percent_policy()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var settings = await store.Db.SystemSettings.AsNoTracking().SingleAsync();
        await Assert.ThrowsAsync<MemberValidationException>(() => new SettingsApplicationService(store.Db).SaveAsync(
            new(settings.RegistrationFeeSatang, 500, settings.WelfarePerMemberSatang, settings.ResetTargetUnits,
                settings.CoverageWaitDays, settings.SpecialNonPayWindowDays, settings.DeathWarningThreshold), FinanceTestStore.Now, "tester"));
        Assert.Equal(400, (await store.Db.SystemSettings.AsNoTracking().SingleAsync()).ServiceFeeBasisPoints);
    }

    [Fact]
    public async Task Active_accounting_rejects_unit_reset_without_creating_paid_balances()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        member.AdvanceUnitsBalance = 2;
        await store.Db.SaveChangesAsync();
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open", []), FinanceTestStore.Now, "tester");
        await Assert.ThrowsAsync<MemberValidationException>(() => new AdvanceResetService(store.Db).ResetAsync("manual", "reset", FinanceTestStore.Date, FinanceTestStore.Now, "tester"));
        Assert.Equal(0, (await store.Db.Members.AsNoTracking().SingleAsync()).AdvanceUnitsBalance);
        Assert.Empty(await store.Db.AdvanceResetBatches.ToListAsync());
        Assert.Empty(await store.Db.AccountingJournals.ToListAsync());
    }

    [Fact]
    public async Task Registration_after_cutover_starts_unpaid_until_actual_receipt()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        store.Db.NumberSequences.Add(new() { SequenceKey = "member_run_no", NextValue = 1, Width = 5 });
        await store.Db.SaveChangesAsync();
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open", []), FinanceTestStore.Now, "tester");
        var member = await new MemberApplicationService(store.Db).RegisterAsync(RegistrationAgeTests.Command(new(1980, 1, 1)),
            FinanceTestStore.Date, FinanceTestStore.Now, "tester");
        Assert.Equal(0, member.AdvanceUnitsBalance);
        Assert.DoesNotContain(await store.Db.AdvanceLedgerEntries.ToListAsync(), x => x.UnitsDelta != 0);
        Assert.Empty(await store.Db.AccountingJournals.ToListAsync());
    }

    [Fact]
    public async Task Failed_death_accounting_cannot_persist_deceased_status_on_later_save()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001");
        await store.AddMemberAsync("00002");
        await new FinanceOperationService(store.Db).OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open", []), FinanceTestStore.Now, "tester");
        await Assert.ThrowsAsync<AccountingValidationException>(() => new DeathApplicationService(store.Db).ConfirmAsync(
            new("00001", "DC-1", FinanceTestStore.Date, "เหตุ", false, null,
                new("certificate.pdf", "application/pdf", "%PDF-1.4\nmock\n%%EOF"u8.ToArray())), FinanceTestStore.Date, FinanceTestStore.Now, "tester"));
        await store.Db.SaveChangesAsync();
        store.Db.ChangeTracker.Clear();
        Assert.Equal(MemberStatus.Normal, (await store.Db.Members.SingleAsync(x => x.Id == member.Id)).Status);
        Assert.Empty(await store.Db.DeathCases.ToListAsync());
        Assert.Empty(await store.Db.AccountingJournals.ToListAsync());
    }
}
