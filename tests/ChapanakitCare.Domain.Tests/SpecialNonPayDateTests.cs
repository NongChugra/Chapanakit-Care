using ChapanakitCare.Domain;

namespace ChapanakitCare.Domain.Tests;

public sealed class SpecialNonPayDateTests
{
    [Theory]
    [InlineData("2026-01-01", false)]
    [InlineData("2026-01-02", true)]
    [InlineData("2027-01-01", true)]
    [InlineData("2027-01-02", false)]
    public void Serious_illness_option_uses_certificate_death_date(string death, bool expected)
    {
        Assert.Equal(expected, SpecialNonPayEligibility.WithinWindow(
            DateOnly.ParseExact(death, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            new DateOnly(2026, 1, 2), 365));
    }
}
