using System.Text.RegularExpressions;

namespace CareerConnect.Domain.Common;

/// <summary>
/// Converts Vietnamese tax codes to the canonical digits-only representation
/// used by the application and the database unique index.
/// </summary>
public static class TaxCodeNormalizer
{
    private static readonly Regex Separators = new(@"[\s.-]", RegexOptions.Compiled);
    private static readonly Regex ValidTaxCode = new(@"\A[0-9]{10}(?:[0-9]{3})?\z", RegexOptions.Compiled);

    public static string? Normalize(string? taxCode)
    {
        if (string.IsNullOrWhiteSpace(taxCode))
        {
            return null;
        }

        return Separators.Replace(taxCode.Trim(), string.Empty);
    }

    public static bool IsValid(string? taxCode)
    {
        var normalized = Normalize(taxCode);
        return normalized is not null && ValidTaxCode.IsMatch(normalized);
    }
}
