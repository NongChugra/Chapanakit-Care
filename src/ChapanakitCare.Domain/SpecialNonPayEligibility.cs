namespace ChapanakitCare.Domain;

public static class SpecialNonPayEligibility
{
    public static bool WithinWindow(DateOnly deathCertificateDate, DateOnly coverageStartDate, int windowDays)
    {
        var elapsedDays = deathCertificateDate.DayNumber - coverageStartDate.DayNumber;
        return windowDays > 0 && elapsedDays >= 0 && elapsedDays < windowDays;
    }
}
