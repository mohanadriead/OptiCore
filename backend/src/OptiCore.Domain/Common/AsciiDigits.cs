namespace OptiCore.Domain.Common;

internal static class AsciiDigits
{
    public static string Required(string? value, int length, string field)
    {
        var normalized = value?.Trim();
        if (normalized is null || normalized.Length != length || normalized.Any(character => character is < '0' or > '9'))
            throw new ArgumentException($"{field} must contain exactly {length} ASCII digits.");
        return normalized;
    }

    public static void Optional(string? value, int length, string field)
    {
        if (!string.IsNullOrWhiteSpace(value)) Required(value, length, field);
    }
}
