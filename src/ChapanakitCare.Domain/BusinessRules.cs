namespace ChapanakitCare.Domain;

public static class CoveragePolicy
{
    public static DateOnly CalculateStart(DateOnly registrationDate, int waitDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(waitDays);
        // Registration is day 1, so a 180-day wait first becomes payable on day 181.
        return registrationDate.AddDays(waitDays);
    }
}

public static class ThaiBuddhistDate
{
    private const int BuddhistEraOffset = 543;

    public static string Format(DateOnly value) => $"{value.Day:00}/{value.Month:00}/{value.Year + BuddhistEraOffset:0000}";

    public static DateOnly Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parts = value.Split('/');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var day) || !int.TryParse(parts[1], out var month) || !int.TryParse(parts[2], out var buddhistYear))
        {
            throw new FormatException("Date must use the format วว/ดด/ปปปป.");
        }

        return new DateOnly(buddhistYear - BuddhistEraOffset, month, day);
    }

}

public static class AgeCalculator
{
    public static int CompletedYears(DateOnly birthDate, DateOnly asOfDate)
    {
        if (birthDate > asOfDate)
        {
            throw new ArgumentOutOfRangeException(nameof(birthDate), "Birth date cannot be after the calculation date.");
        }

        var age = asOfDate.Year - birthDate.Year;
        if (birthDate.AddYears(age) > asOfDate)
        {
            age--;
        }

        return age;
    }
}

public enum DeathEligibility
{
    BeforeCoverageZero,
    Payable,
    ManualNonPayZero
}

public sealed record DeathBenefitInput(
    DateOnly RecordedDate,
    DateOnly CoverageStartDate,
    bool IsManualNonPayCase,
    int OtherLivingMemberCount,
    long WelfarePerMemberSatang,
    int ServiceFeeBasisPoints,
    int DeceasedAdvanceUnits,
    int BeneficiaryCount,
    long? DeceasedAdvanceBalanceSatang = null);

public sealed record DeathBenefitResult(
    DeathEligibility Eligibility,
    bool ShouldDecrementContributors,
    int ContributorCount,
    long GrossCollectionSatang,
    long ServiceFeeSatang,
    long NetCollectionSatang,
    long DeceasedAdvanceValueSatang,
    long TotalBenefitSatang,
    IReadOnlyList<long> BeneficiarySharesSatang);

public static class DeathBenefitCalculator
{
    private const int BasisPointDenominator = 10_000;

    public static DeathBenefitResult Calculate(DeathBenefitInput input)
    {
        Validate(input);

        var eligibility = input.RecordedDate < input.CoverageStartDate
            ? DeathEligibility.BeforeCoverageZero
            : input.IsManualNonPayCase
                ? DeathEligibility.ManualNonPayZero
                : DeathEligibility.Payable;

        if (eligibility != DeathEligibility.Payable)
        {
            return new DeathBenefitResult(
                eligibility,
                ShouldDecrementContributors: false,
                ContributorCount: 0,
                GrossCollectionSatang: 0,
                ServiceFeeSatang: 0,
                NetCollectionSatang: 0,
                DeceasedAdvanceValueSatang: 0,
                TotalBenefitSatang: 0,
                BeneficiarySharesSatang: ZeroShares(input.BeneficiaryCount));
        }

        var grossCollection = checked(input.OtherLivingMemberCount * input.WelfarePerMemberSatang);
        var feeNumerator = checked(grossCollection * input.ServiceFeeBasisPoints);
        // The association records the deduction in whole baht. Calculate in satang,
        // then discard any sub-baht remainder so the displayed amount always ends in .00.
        var serviceFee = (feeNumerator / BasisPointDenominator / 100) * 100;
        var netCollection = checked(grossCollection - serviceFee);
        // Active accounting supplies the recorded monetary balance. First-day callers
        // still use their unit counters until an explicit accounting cutover occurs.
        var advanceValue = input.DeceasedAdvanceBalanceSatang
            ?? checked(input.DeceasedAdvanceUnits * input.WelfarePerMemberSatang);
        var totalBenefit = checked(netCollection + advanceValue);

        return new DeathBenefitResult(
            eligibility,
            ShouldDecrementContributors: true,
            ContributorCount: input.OtherLivingMemberCount,
            GrossCollectionSatang: grossCollection,
            ServiceFeeSatang: serviceFee,
            NetCollectionSatang: netCollection,
            DeceasedAdvanceValueSatang: advanceValue,
            TotalBenefitSatang: totalBenefit,
            BeneficiarySharesSatang: SplitWithRemainderToFirst(totalBenefit, input.BeneficiaryCount));
    }

    private static void Validate(DeathBenefitInput input)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(input.OtherLivingMemberCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.WelfarePerMemberSatang);

        if (input.ServiceFeeBasisPoints is < 0 or > BasisPointDenominator)
        {
            throw new ArgumentOutOfRangeException(nameof(input.ServiceFeeBasisPoints));
        }

        if (input.BeneficiaryCount is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(input.BeneficiaryCount));
        }
    }

    private static IReadOnlyList<long> ZeroShares(int beneficiaryCount) =>
        beneficiaryCount switch
        {
            0 => Array.Empty<long>(),
            1 => [0L],
            2 => [0L, 0L],
            _ => throw new ArgumentOutOfRangeException(nameof(beneficiaryCount))
        };

    private static IReadOnlyList<long> SplitWithRemainderToFirst(long total, int beneficiaryCount)
    {
        if (beneficiaryCount == 0)
        {
            return Array.Empty<long>();
        }

        if (beneficiaryCount == 1)
        {
            return [total];
        }

        var secondShare = total / 2;
        return [total - secondShare, secondShare];
    }
}
