using System.Globalization;
using System.Text.RegularExpressions;

namespace ChapanakitCare.Domain;

public static class FinanceMoney
{
    public static long Parse(string text, bool allowZero = false)
    {
        var value = text?.Trim() ?? string.Empty;
        if (!Regex.IsMatch(value, @"^(?:[0-9]+|[1-9][0-9]{0,2}(?:,[0-9]{3})+)(?:\.[0-9]{1,2})?$", RegexOptions.CultureInvariant)
            || !decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var baht)
            || baht > long.MaxValue / 100m || baht < 0 || (!allowZero && baht == 0))
            throw new FormatException("กรุณาระบุจำนวนเงินบาทที่ถูกต้อง ทศนิยมไม่เกิน 2 ตำแหน่ง");
        return (long)(baht * 100m);
    }

    public static string Format(long satang) => (satang / 100m).ToString("N2", CultureInfo.InvariantCulture);
}
