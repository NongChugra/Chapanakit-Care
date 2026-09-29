using ChapanakitCare.Domain;

namespace ChapanakitCare.Domain.Tests;

public sealed class FinanceMoneyTests
{
    [Theory]
    [InlineData("1,234.50", 123450)]
    [InlineData("9", 900)]
    [InlineData("0.01", 1)]
    [InlineData(" 451.2 ", 45120)]
    public void Parses_human_baht_input_without_floating_point_rounding(string input, long expected)
        => Assert.Equal(expected, FinanceMoney.Parse(input));

    [Theory]
    [InlineData("1.001")]
    [InlineData("1e3")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("12,34.50")]
    [InlineData("NaN")]
    [InlineData("92233720368547758.08")]
    public void Rejects_fractional_satang_malformed_grouping_and_out_of_range_input(string input)
        => Assert.Throws<FormatException>(() => FinanceMoney.Parse(input));

    [Fact]
    public void Zero_is_allowed_only_for_explicit_opening_fields()
        => Assert.Equal(0, FinanceMoney.Parse("0.00", allowZero: true));

    [Theory]
    [InlineData(-451, "-4.51")]
    [InlineData(123456, "1,234.56")]
    [InlineData(0, "0.00")]
    [InlineData(long.MinValue, "-92,233,720,368,547,758.08")]
    public void Formats_signed_satang_without_losing_precision(long satang, string expected)
        => Assert.Equal(expected, FinanceMoney.Format(satang));
}
