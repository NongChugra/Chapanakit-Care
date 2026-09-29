using System.Text;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.AccountingOperations;
using ChapanakitCare.Infrastructure.AccountingReports;
using ChapanakitCare.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;

namespace ChapanakitCare.Domain.Tests;

public sealed class CollectionReportTests
{
    [Fact]
    public async Task Balance_report_uses_frozen_collection_snapshot_and_net_posted_allocations()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001", "07");
        var request = Assert.Single(await new WelfareCollectionService(store.Db).CreateAsync(new(
            "2569-09", FinanceTestStore.Date, FinanceTestStore.Date.AddDays(18), "เรียกเก็บเงินสงเคราะห์",
            "collection-request", [new(member.Id, 1_000)]), FinanceTestStore.Now, "tester"));
        await new FinanceOperationService(store.Db).OpenAsync(new(
            "welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open-welfare", []), FinanceTestStore.Now, "tester");
        await new FinanceOperationService(store.Db).ReceiveAsync(new(
            FinanceTestStore.Date, "1000", "รับเงินจริงบางส่วน", "collection-receipt", [new(member.Id, 401, request.Id)]),
            FinanceTestStore.Now, "tester");
        member.GroupNo = "99";
        member.FirstName = "ชื่อใหม่";
        await store.Db.SaveChangesAsync();

        var report = await new CollectionReportService(store.Db).GetBalancesAsync(FinanceTestStore.Date);

        var row = Assert.Single(report.Rows);
        Assert.Equal(request.Id, row.CollectionId);
        Assert.Equal("07", row.GroupNo);
        Assert.Equal(request.MemberName, row.MemberName);
        Assert.Equal(1_000, row.RequestedSatang);
        Assert.Equal(401, row.PaidSatang);
        Assert.Equal(599, row.OutstandingSatang);
        Assert.Equal(1_000, report.TotalRequestedSatang);
        Assert.Equal(401, report.TotalPaidSatang);
        Assert.Equal(599, report.TotalOutstandingSatang);
    }

    [Fact]
    public async Task Balance_report_honors_business_date_cutoffs_for_requests_and_reversals()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var member = await store.AddMemberAsync("00001", "07");
        var request = Assert.Single(await new WelfareCollectionService(store.Db).CreateAsync(new(
            "2569-09", FinanceTestStore.Date, FinanceTestStore.Date.AddDays(18), "เรียกเก็บเงินสงเคราะห์",
            "collection-request", [new(member.Id, 1_000)]), FinanceTestStore.Now, "tester"));
        var finance = new FinanceOperationService(store.Db);
        await finance.OpenAsync(new("welfare", FinanceTestStore.Date, "ยอดเริ่มต้นศูนย์", "open-welfare", []), FinanceTestStore.Now, "tester");
        var receipt = await finance.ReceiveAsync(new(FinanceTestStore.Date, "1000", "รับเงินจริง", "collection-receipt",
            [new(member.Id, 1_000, request.Id)]), FinanceTestStore.Now, "tester");
        await finance.ReverseAsync(new(receipt, FinanceTestStore.Date.AddDays(1), "ยกเลิกรายการรับเงิน", "reverse-receipt"),
            FinanceTestStore.Now, "tester");

        var beforeRequest = await new CollectionReportService(store.Db).GetBalancesAsync(FinanceTestStore.Date.AddDays(-1));
        var beforeReversal = await new CollectionReportService(store.Db).GetBalancesAsync(FinanceTestStore.Date);
        var afterReversal = await new CollectionReportService(store.Db).GetBalancesAsync(FinanceTestStore.Date.AddDays(1));

        Assert.Empty(beforeRequest.Rows);
        Assert.Equal(1_000, Assert.Single(beforeReversal.Rows).PaidSatang);
        Assert.Equal(0, Assert.Single(beforeReversal.Rows).OutstandingSatang);
        Assert.Equal(0, Assert.Single(afterReversal.Rows).PaidSatang);
        Assert.Equal(1_000, Assert.Single(afterReversal.Rows).OutstandingSatang);
    }

    [Fact]
    public async Task Collection_documents_are_read_only_and_reminders_include_only_due_outstanding_rows()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var due = await store.AddMemberAsync("00001", "07");
        var later = await store.AddMemberAsync("00002", "07");
        await new WelfareCollectionService(store.Db).CreateAsync(new(
            "2569-09", FinanceTestStore.Date, FinanceTestStore.Date, "เรียกเก็บเงินสงเคราะห์",
            "collection-request-due", [new(due.Id, 1_000)]), FinanceTestStore.Now, "tester");
        await new WelfareCollectionService(store.Db).CreateAsync(new(
            "2569-10", FinanceTestStore.Date, FinanceTestStore.Date.AddDays(1), "เรียกเก็บเงินสงเคราะห์",
            "collection-request-later", [new(later.Id, 2_000)]), FinanceTestStore.Now, "tester");

        var journalsBefore = await store.Db.AccountingJournals.CountAsync();
        var report = await new CollectionReportService(store.Db).GetBalancesAsync(FinanceTestStore.Date);
        var collectionSheet = CollectionReportDocuments.ToCollectionSheet(report);
        var reminder = CollectionReportDocuments.ToReminderDocument(report);
        var pdf = AccountingDocumentRenderer.GeneratePdf(collectionSheet);
        var csv = Encoding.UTF8.GetString(AccountingDocumentRenderer.GenerateCsv(reminder));

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf));
        Assert.Contains("รายการแจ้งเตือนยอดค้างชำระ", csv);
        Assert.Contains(due.RunNo, csv);
        Assert.DoesNotContain(later.RunNo, csv);
        Assert.Equal(journalsBefore, await store.Db.AccountingJournals.CountAsync());
    }

    [Fact]
    public async Task Balance_report_filters_by_frozen_cycle_and_group_snapshots()
    {
        await using var store = await FinanceTestStore.CreateAsync();
        var groupSeven = await store.AddMemberAsync("00001", "07");
        var groupEight = await store.AddMemberAsync("00002", "08");
        var collections = new WelfareCollectionService(store.Db);
        await collections.CreateAsync(new(
            "2569-09", FinanceTestStore.Date, FinanceTestStore.Date.AddDays(10), "เรียกเก็บเงินสงเคราะห์",
            "collection-filter-seven", [new(groupSeven.Id, 1_000)]), FinanceTestStore.Now, "tester");
        await collections.CreateAsync(new(
            "2569-10", FinanceTestStore.Date, FinanceTestStore.Date.AddDays(10), "เรียกเก็บเงินสงเคราะห์",
            "collection-filter-eight", [new(groupEight.Id, 2_000)]), FinanceTestStore.Now, "tester");

        var report = await new CollectionReportService(store.Db).GetBalancesAsync(
            FinanceTestStore.Date, "2569-09", "07");

        var row = Assert.Single(report.Rows);
        Assert.Equal("2569-09", row.CycleKey);
        Assert.Equal("07", row.GroupNo);
        Assert.Equal(groupSeven.RunNo, row.MemberRunNo);
        Assert.Equal(1_000, report.TotalOutstandingSatang);
    }

    [Fact]
    public void Outstanding_document_excludes_settled_rows_and_keeps_only_open_amounts()
    {
        var asOf = new DateOnly(2026, 9, 12);
        var openId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var settledId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var report = new WelfareCollectionBalanceReport(asOf, null, null,
        [
            new(openId, Guid.Empty, "00001", "สมาชิกค้าง", "07", "2569-09", asOf, asOf,
                1_000, 400, 600),
            new(settledId, Guid.Empty, "00002", "สมาชิกชำระแล้ว", "07", "2569-09", asOf, asOf,
                1_000, 1_000, 0)
        ], 2_000, 1_400, 600);

        var document = CollectionReportDocuments.ToOutstandingDocument(report);

        Assert.Single(document.Rows);
        Assert.Contains("00001", document.Rows[0]);
        Assert.DoesNotContain(document.Rows.SelectMany(row => row), value => value.Contains("00002", StringComparison.Ordinal));
        Assert.Contains(document.Notes!, note => note.Contains("6.00", StringComparison.Ordinal));
    }
}
