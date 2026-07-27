using System.Text;

namespace Fatture.Web.Services;

public static class IdentityNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var result = new StringBuilder();
        foreach (var c in value.ToUpperInvariant())
            if (char.IsLetterOrDigit(c)) result.Append(c);
        return result.ToString();
    }

    public static string NormalizeVat(string? country, string? number)
    {
        var normalizedCountry = Normalize(country);
        var normalizedNumber = Normalize(number);
        if (normalizedNumber.StartsWith("IT", StringComparison.Ordinal) &&
            (normalizedCountry.Length == 0 || normalizedCountry == "IT"))
            normalizedNumber = normalizedNumber[2..];
        return normalizedCountry == "IT" ? normalizedNumber : normalizedCountry + normalizedNumber;
    }
}
