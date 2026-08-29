using ChapanakitCare.Domain.Entities;

namespace ChapanakitCare.Infrastructure.Deaths;

public sealed record DeathRegistryBeneficiaryColumns(string First, string Second)
{
    public static DeathRegistryBeneficiaryColumns From(IReadOnlyList<DeathBeneficiarySnapshot> beneficiaries)
    {
        var ordered = beneficiaries.OrderBy(value => value.SlotNo).Take(2).Select(Name).ToArray();
        return new DeathRegistryBeneficiaryColumns(
            ordered.ElementAtOrDefault(0) ?? "–",
            ordered.ElementAtOrDefault(1) ?? "–");
    }

    private static string Name(DeathBeneficiarySnapshot value) =>
        $"{value.Title}{value.FirstName} {value.LastName}".Trim();
}
