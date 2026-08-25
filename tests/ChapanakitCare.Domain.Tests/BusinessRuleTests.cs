using ChapanakitCare.Domain;
using System.Globalization;

namespace ChapanakitCare.Domain.Tests;

public sealed class BusinessRuleTests
{
    [Fact]
    public void Coverage_starts_exactly_180_calendar_days_after_approval()
    {
        var approval = new DateOnly(2026, 8, 25);

        var coverage = CoveragePolicy.CalculateStart(approval, waitDays: 180);

        Assert.Equal(new DateOnly(2027, 2, 21), coverage);
    }

    [Theory]
    [InlineData("1980-08-24", "2026-08-25", 46)]
    [InlineData("1980-08-25", "2026-08-25", 46)]
    [InlineData("1980-08-26", "2026-08-25", 45)]
    public void Age_is_completed_years_on_the_requested_business_date(
        string birthDateText,
        string asOfText,
        int expectedAge)
    {
        var age = AgeCalculator.CompletedYears(
            DateOnly.ParseExact(birthDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateOnly.ParseExact(asOfText, "yyyy-MM-dd", CultureInfo.InvariantCulture));

        Assert.Equal(expectedAge, age);
    }

    [Fact]
    public void Death_on_coverage_start_is_payable_and_excludes_the_deceased_from_contributors()
    {
        var result = DeathBenefitCalculator.Calculate(new DeathBenefitInput(
            RecordedDate: new DateOnly(2027, 2, 21),
            CoverageStartDate: new DateOnly(2027, 2, 21),
            IsManualNonPayCase: false,
            OtherLivingMemberCount: 100,
            WelfarePerMemberSatang: 1_500,
            ServiceFeeBasisPoints: 400,
            DeceasedAdvanceUnits: 30,
            BeneficiaryCount: 1));

        Assert.Equal(DeathEligibility.Payable, result.Eligibility);
        Assert.True(result.ShouldDecrementContributors);
        Assert.Equal(100, result.ContributorCount);
        Assert.Equal(150_000, result.GrossCollectionSatang);
        Assert.Equal(6_000, result.ServiceFeeSatang);
        Assert.Equal(144_000, result.NetCollectionSatang);
        Assert.Equal(45_000, result.DeceasedAdvanceValueSatang);
        Assert.Equal(189_000, result.TotalBenefitSatang);
        Assert.Equal([189_000L], result.BeneficiarySharesSatang);
    }

    [Theory]
    [InlineData(false, "2027-02-20", DeathEligibility.BeforeCoverageZero)]
    [InlineData(true, "2027-02-21", DeathEligibility.ManualNonPayZero)]
    public void Nonpay_death_has_zero_money_and_does_not_move_other_member_counters(
        bool manualNonPay,
        string recordedDateText,
        DeathEligibility expectedEligibility)
    {
        var result = DeathBenefitCalculator.Calculate(new DeathBenefitInput(
            RecordedDate: DateOnly.ParseExact(recordedDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            CoverageStartDate: new DateOnly(2027, 2, 21),
            IsManualNonPayCase: manualNonPay,
            OtherLivingMemberCount: 100,
            WelfarePerMemberSatang: 1_500,
            ServiceFeeBasisPoints: 400,
            DeceasedAdvanceUnits: 30,
            BeneficiaryCount: 2));

        Assert.Equal(expectedEligibility, result.Eligibility);
        Assert.False(result.ShouldDecrementContributors);
        Assert.Equal(0, result.ContributorCount);
        Assert.Equal(0, result.TotalBenefitSatang);
        Assert.Equal([0L, 0L], result.BeneficiarySharesSatang);
    }

    [Fact]
    public void Service_fee_rounds_up_to_the_next_satang_and_first_beneficiary_gets_remainder()
    {
        var result = DeathBenefitCalculator.Calculate(new DeathBenefitInput(
            RecordedDate: new DateOnly(2027, 2, 21),
            CoverageStartDate: new DateOnly(2027, 2, 21),
            IsManualNonPayCase: false,
            OtherLivingMemberCount: 1,
            WelfarePerMemberSatang: 1_501,
            ServiceFeeBasisPoints: 333,
            DeceasedAdvanceUnits: 0,
            BeneficiaryCount: 2));

        Assert.Equal(50, result.ServiceFeeSatang);
        Assert.Equal(1_451, result.TotalBenefitSatang);
        Assert.Equal([726L, 725L], result.BeneficiarySharesSatang);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Beneficiary_count_outside_zero_to_two_is_rejected(int beneficiaryCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DeathBenefitCalculator.Calculate(
            new DeathBenefitInput(
                new DateOnly(2027, 2, 21),
                new DateOnly(2027, 2, 21),
                false,
                1,
                1_500,
                400,
                30,
                beneficiaryCount)));
    }
}
