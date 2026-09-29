using ChapanakitCare.Domain;

namespace ChapanakitCare.Domain.Tests;

public sealed class AccountingBenefitTests
{
    [Fact]
    public void Actual_prepaid_money_survives_partial_units_and_is_split_without_loss()
    {
        // Three contributions of 9 baht: 27 gross, 1 fee after whole-baht rounding,
        // 26 net + 4.51 actual prepaid money = 30.51. A unit counter cannot retain 4.51.
        var result = DeathBenefitCalculator.Calculate(new DeathBenefitInput(
            new DateOnly(2026, 9, 12), new DateOnly(2026, 1, 1), false,
            3, 900, 400, 0, 2, DeceasedAdvanceBalanceSatang: 451));

        Assert.Equal(100, result.ServiceFeeSatang);
        Assert.Equal(451, result.DeceasedAdvanceValueSatang);
        Assert.Equal(3051, result.TotalBenefitSatang);
        Assert.Equal(new long[] { 1526, 1525 }, result.BeneficiarySharesSatang);
    }

    [Fact]
    public void Actual_recorded_shortfall_reduces_benefit_without_revaluing_old_units()
    {
        var result = DeathBenefitCalculator.Calculate(new DeathBenefitInput(
            new DateOnly(2026, 9, 12), new DateOnly(2026, 1, 1), false,
            3, 900, 400, -1, 1, DeceasedAdvanceBalanceSatang: -450));

        Assert.Equal(-450, result.DeceasedAdvanceValueSatang);
        Assert.Equal(2150, result.TotalBenefitSatang);
    }

    [Fact]
    public void Nonpay_case_does_not_turn_prepaid_money_into_a_benefit_automatically()
    {
        var result = DeathBenefitCalculator.Calculate(new DeathBenefitInput(
            new DateOnly(2026, 9, 12), new DateOnly(2026, 1, 1), true,
            3, 900, 400, 30, 1, DeceasedAdvanceBalanceSatang: 27000));

        Assert.Equal(0, result.ServiceFeeSatang);
        Assert.Equal(0, result.TotalBenefitSatang);
    }
}
