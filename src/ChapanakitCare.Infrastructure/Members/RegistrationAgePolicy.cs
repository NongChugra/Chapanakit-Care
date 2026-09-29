using ChapanakitCare.Domain;

namespace ChapanakitCare.Infrastructure.Members;

public static class RegistrationAgePolicy
{
    public const string Message = "ผู้สมัครต้องมีอายุ 20–60 ปีบริบูรณ์ ณ วันที่สมัคร";

    public static bool IsEligible(DateOnly? birthDate, DateOnly registrationDate) =>
        birthDate is { } birth && birth <= registrationDate &&
        AgeCalculator.CompletedYears(birth, registrationDate) is >= 20 and <= 60;
}
