using System.Net;
using System.Text.RegularExpressions;
using ChapanakitCare.Domain.Entities;
using ChapanakitCare.Infrastructure.Persistence;
using ChapanakitCare.Infrastructure.Reports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChapanakitCare.Domain.Tests;

public sealed class ReportCompletionDataTests
{
    [Fact]
    public async Task Monthly_summary_excludes_exits_before_the_period_and_counts_a_confirmed_death_once()
    {
        await using var database = await ReportDatabase.CreateAsync();
        var period = new ReportPeriod(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var active = Member("00001", new DateOnly(2026, 1, 5), MemberStatus.Normal);
        var resignedBeforePeriod = Member("00002", new DateOnly(2026, 1, 6), MemberStatus.Resigned);
        var resignedDuringPeriod = Member("00003", new DateOnly(2026, 1, 7), MemberStatus.Resigned);
        var diedDuringPeriod = Member("00004", new DateOnly(2026, 1, 8), MemberStatus.Deceased);
        var addedDuringPeriod = Member("00005", new DateOnly(2026, 3, 9), MemberStatus.Normal);
        var archived = Member("00006", new DateOnly(2026, 1, 10), MemberStatus.Normal);
        archived.ArchivedAtUtc = Now;
        archived.ArchivedBy = "tester";
        archived.ArchiveReason = "fixture archive";
        database.Context.Members.AddRange(active, resignedBeforePeriod, resignedDuringPeriod, diedDuringPeriod, addedDuringPeriod, archived);
        database.Context.MemberStatusEvents.AddRange(
            StatusEvent(resignedBeforePeriod.Id, "resigned", new DateOnly(2026, 2, 28)),
            StatusEvent(resignedDuringPeriod.Id, "resigned", new DateOnly(2026, 3, 10)));

        var deathId = Guid.NewGuid();
        database.Context.MemberStatusEvents.Add(StatusEvent(diedDuringPeriod.Id, "deceased", new DateOnly(2026, 3, 20), deathId));
        database.Context.DeathCases.Add(ConfirmedDeath(deathId, diedDuringPeriod.Id, new DateOnly(2026, 3, 20)));
        await database.Context.SaveChangesAsync();

        var pdf = await new ReportApplicationService(database.Context).GenerateMonthlySummaryAsync(period);
        var compactText = Regex.Replace(ReportPdfText.Extract(pdf), @"\s", "");

        // Opening: active, the two members who exit this month, and the later-added
        // member is excluded. The February resignation and archived member are not open.
        // March then adds one, records one resignation and one death, leaving two.
        Assert.Contains("311102", compactText);
    }

    [Fact]
    public void Member_by_manager_rows_keep_each_beneficiary_relationship_on_the_matching_row()
    {
        var rows = MemberByManagerReportRows.Expand(new MemberByManagerReportSource(
            "00001", "นาย", "สมาชิก", "ทดสอบ", "1234567890123",
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 2), new DateOnly(2026, 9, 1),
            new DateOnly(1980, 1, 1), "1 หมู่ 1", ["นาง ผู้รับหนึ่ง", "นาย ผู้รับสอง"],
            BeneficiaryRelationships: ["บุตร"]));

        Assert.Equal("บุตร", rows[0].BeneficiaryRelationship);
        Assert.Equal("-", rows[1].BeneficiaryRelationship);
        Assert.Equal("", rows[1].MemberName);
    }

    [Fact]
    public void Sak_one_uses_an_explicit_spouse_placeholder_when_the_source_has_no_spouse_field()
    {
        var row = new SakOneReportRow("1", "นาย สมาชิก", "00001", "01/03/2569", "-", "-", "-", "-", "-", "-", "-", "-", "-", "-", "-");

        Assert.Equal("-", row.Spouse);
    }

    [Fact]
    public async Task Group_options_keep_the_exact_group_number_and_show_the_current_leader_or_vacancy()
    {
        await using var database = await ReportDatabase.CreateAsync();
        var leader = Member("00011", new DateOnly(2026, 1, 1), MemberStatus.Normal);
        leader.GroupNo = "010";
        leader.FirstName = "หัวหน้า";
        leader.LastName = "กลุ่ม";
        var vacancy = Member("00012", new DateOnly(2026, 1, 2), MemberStatus.Normal);
        vacancy.GroupNo = "0100";
        database.Context.Members.AddRange(leader, vacancy);
        database.Context.CoordinatorPositions.Add(new CoordinatorPosition
        {
            PositionKey = "group:010",
            RoleCode = "group_leader",
            GroupNo = "010",
            MemberId = leader.Id,
            AppointedOn = new DateOnly(2026, 2, 1),
            Version = 1
        });
        await database.Context.SaveChangesAsync();

        var service = new ReportApplicationService(database.Context);
        var groups = await service.GetManagerGroupOptionsAsync();

        Assert.Equal(["010", "0100"], groups.Select(group => group.GroupNo));
        Assert.Equal("นายหัวหน้า กลุ่ม", groups.Single(group => group.GroupNo == "010").LeaderName);
        Assert.Null(groups.Single(group => group.GroupNo == "0100").LeaderName);
        Assert.Equal(["010", "0100"], await service.GetManagerGroupsAsync());
        var pdf = Regex.Replace(ReportPdfText.Extract(await service.GenerateMemberByManagerAsync("010")), @"\s", "");
        Assert.Contains("หัวหน้ากลุ่ม:นายหัวหน้ากลุ่ม", pdf);
        Assert.DoesNotContain("00012", pdf);
    }

    [Fact]
    public async Task Available_relationships_and_membership_exit_are_printed_from_saved_data()
    {
        await using var database = await ReportDatabase.CreateAsync();
        var member = Member("00020", new DateOnly(2026, 3, 1), MemberStatus.Resigned);
        database.Context.Members.Add(member);
        database.Context.MemberStatusEvents.Add(StatusEvent(member.Id, "resigned", new DateOnly(2026, 3, 20)));
        var beneficiary = TestData.Beneficiary(1);
        beneficiary.MemberId = member.Id;
        beneficiary.Relationship = "หลาน";
        database.Context.MemberBeneficiaries.Add(beneficiary);
        await database.Context.SaveChangesAsync();
        var service = new ReportApplicationService(database.Context);
        Assert.Contains("หลาน", ReportPdfText.Extract(await service.GenerateAllMembersAsync()));
        var pdf = ReportPdfText.Extract(await service.GenerateSakOneAsync(new(new(2026, 3, 1), new(2026, 3, 31))));
        Assert.Contains("20/03/2569", pdf);
        Assert.Contains("ลาออก", pdf);
    }

    [Fact]
    public async Task Sak_one_includes_existing_member_changes_recorded_during_the_period()
    {
        await using var database = await ReportDatabase.CreateAsync();
        var member = Member("00031", new DateOnly(2026, 1, 5), MemberStatus.Normal);
        database.Context.Members.Add(member);
        var audit = new AuditEvent
        {
            Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), MemberId = member.Id,
            Action = "member.updated", EntityType = "member", EntityId = member.Id.ToString(),
            OccurredAtUtc = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero), ActorDisplayName = "tester"
        };
        database.Context.AuditEvents.Add(audit);
        database.Context.AuditFieldChanges.Add(new AuditFieldChange
        {
            Id = Guid.NewGuid(), AuditEventId = audit.Id, FieldName = "FirstName",
            OldValueDisplay = "เดิม", NewValueDisplay = "ใหม่"
        });
        await database.Context.SaveChangesAsync();
        var pdf = ReportPdfText.Extract(await new ReportApplicationService(database.Context)
            .GenerateSakOneAsync(new(new(2026, 3, 1), new(2026, 3, 31))));
        Assert.Contains("00031", pdf);
        Assert.Contains("10/03/2569", pdf);
        Assert.Contains("เดิม", pdf);
        Assert.Contains("ใหม่", pdf);
    }

    [Fact]
    public async Task Sak_one_keeps_long_change_history_without_failing_page_layout()
    {
        await using var database = await ReportDatabase.CreateAsync();
        var member = Member("00032", new DateOnly(2026, 3, 1), MemberStatus.Normal);
        database.Context.Members.Add(member);
        var audit = new AuditEvent
        {
            Id = Guid.NewGuid(), OperationId = Guid.NewGuid(), MemberId = member.Id,
            Action = "member.updated", EntityType = "member", EntityId = member.Id.ToString(),
            OccurredAtUtc = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero), ActorDisplayName = "tester"
        };
        database.Context.AuditEvents.Add(audit);
        for (var index = 0; index < 100; index++) database.Context.AuditFieldChanges.Add(new AuditFieldChange
        {
            Id = Guid.NewGuid(), AuditEventId = audit.Id, FieldName = "FirstName",
            OldValueDisplay = $"ชื่อเดิม{index:D2}", NewValueDisplay = $"ชื่อใหม่{index:D2}"
        });
        await database.Context.SaveChangesAsync();
        var pdf = ReportPdfText.Extract(await new ReportApplicationService(database.Context)
            .GenerateSakOneAsync(new(new(2026, 3, 1), new(2026, 3, 31))));
        Assert.Contains("ชื่อใหม่99", pdf);
    }

    private static readonly DateTimeOffset Now = new(2026, 3, 31, 12, 0, 0, TimeSpan.Zero);

    private static Member Member(string runNo, DateOnly applicationDate, MemberStatus status) => new()
    {
        Id = Guid.NewGuid(),
        RunNo = runNo,
        Title = "นาย",
        FirstName = "สมาชิก",
        LastName = runNo,
        District = "ร้องกวาง",
        Province = "แพร่",
        ApplicationDate = applicationDate,
        ApprovalDate = applicationDate,
        CoverageStartDate = applicationDate.AddMonths(6),
        Status = status,
        AdvanceUnitsBalance = status == MemberStatus.Normal ? 30 : 0,
        Version = 1,
        CreatedAtUtc = Now,
        CreatedBy = "tester",
        UpdatedAtUtc = Now,
        UpdatedBy = "tester"
    };

    private static MemberStatusEvent StatusEvent(Guid memberId, string toStatus, DateOnly effectiveDate, Guid? sourceId = null) => new()
    {
        Id = Guid.NewGuid(),
        MemberId = memberId,
        FromStatus = "normal",
        ToStatus = toStatus,
        EffectiveDate = effectiveDate,
        SourceType = sourceId is null ? "resignation" : "death_case",
        SourceId = sourceId,
        CreatedAtUtc = Now,
        CreatedBy = "tester"
    };

    private static DeathCase ConfirmedDeath(Guid id, Guid memberId, DateOnly businessDate) => new()
    {
        Id = id,
        DeathCaseNo = "D00001",
        DeathSequenceNo = 1,
        MemberId = memberId,
        RecordedBusinessDate = businessDate,
        RecordedAtUtc = Now,
        DeathCertificateNo = "CERT-00001",
        DeathCertificateDate = businessDate,
        DeathCertificateFileName = "certificate.pdf",
        DeathCertificatePdf = [1],
        DeathCertificateSize = 1,
        DeathCertificateSha256 = new string('a', 64),
        CauseOfDeathText = "test",
        EligibilityResult = "payable",
        ConfirmedAtUtc = Now,
        ConfirmedBy = "tester"
    };

    private sealed class ReportDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ReportDatabase(SqliteConnection connection, AppDbContext context)
        {
            this.connection = connection;
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<ReportDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            return new ReportDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}

public sealed class ReportCompletionHttpTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private WebApplication app = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ChapanakitCare.Web.Pages.Reports.IndexModel).Assembly.GetName().Name
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddRazorPages();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddScoped<ReportApplicationService>();
        app = builder.Build();
        app.UseRequestLocalization(new RequestLocalizationOptions()
            .SetDefaultCulture("th-TH").AddSupportedCultures("th-TH").AddSupportedUICultures("th-TH"));
        app.MapRazorPages();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await database.Database.EnsureCreatedAsync();
            var leader = TestData.Member();
            leader.GroupNo = "010";
            leader.Title = "นาย";
            leader.FirstName = "หัวหน้า";
            leader.LastName = "กลุ่ม";
            var vacancy = TestData.Member();
            vacancy.Id = Guid.NewGuid();
            vacancy.RunNo = "00002";
            vacancy.GroupNo = "0100";
            database.Members.AddRange(leader, vacancy);
            database.CoordinatorPositions.Add(new CoordinatorPosition
            {
                PositionKey = "group:010",
                RoleCode = "group_leader",
                GroupNo = "010",
                MemberId = leader.Id,
                AppointedOn = new DateOnly(2026, 8, 25),
                Version = 1
            });
            await database.SaveChangesAsync();
        }

        await app.StartAsync();
        client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    }

    [Fact]
    public async Task Report_page_renders_an_exact_group_option_with_current_leader_or_vacancy()
    {
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Reports"));

        Assert.Matches(@"<option value=""010"">010\s*—\s*นายหัวหน้า กลุ่ม</option>", html);
        Assert.Matches(@"<option value=""0100"">0100\s*—\s*ยังไม่มีหัวหน้ากลุ่ม</option>", html);
    }

    [Theory]
    [InlineData("/Reports?handler=MemberByManager")]
    [InlineData("/Reports?handler=Monthly&month=not-a-month")]
    [InlineData("/Reports?handler=SakOne&month=9999-12")]
    public async Task Missing_or_invalid_report_parameters_return_bad_request_instead_of_a_server_error(string target)
    {
        using var response = await client.GetAsync(target);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (app is not null) await app.DisposeAsync();
        await connection.DisposeAsync();
    }
}
