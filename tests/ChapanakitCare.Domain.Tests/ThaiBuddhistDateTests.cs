using ChapanakitCare.Domain;

namespace ChapanakitCare.Domain.Tests;

public sealed class ThaiBuddhistDateTests
{
    [Fact]
    public void Format_displays_a_gregorian_date_in_buddhist_era()
    {
        Assert.Equal("25/08/2569", ThaiBuddhistDate.Format(new DateOnly(2026, 8, 25)));
    }

    [Fact]
    public void Parse_converts_buddhist_era_input_to_gregorian_date()
    {
        Assert.Equal(new DateOnly(2026, 8, 25), ThaiBuddhistDate.Parse("25/08/2569"));
    }

    [Fact]
    public void Gregorian_storage_year_is_not_guessed_to_be_a_buddhist_year()
    {
        Assert.Equal("01/01/2943", ThaiBuddhistDate.Format(new DateOnly(2400, 1, 1)));
    }
}
