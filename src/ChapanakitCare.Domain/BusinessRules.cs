namespace ChapanakitCare.Domain;

public static class CoveragePolicy
{
    public static DateOnly CalculateStart(DateOnly approvalDate, int waitDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(waitDays);
        return approvalDate.AddDays(waitDays);
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
    int BeneficiaryCount);

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
        // The association keeps whole satang only.  Fractional satang is never collected.
        var serviceFee = feeNumerator / BasisPointDenominator;
        var netCollection = checked(grossCollection - serviceFee);
        var advanceValue = checked(input.DeceasedAdvanceUnits * input.WelfarePerMemberSatang);
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
