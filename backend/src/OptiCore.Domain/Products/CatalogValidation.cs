namespace OptiCore.Domain.Products;

public static class CatalogValidation
{
    public static string? Optional(string? value, int maximum)
    {
        value = value?.Trim();
        if (value?.Length > maximum) throw new ArgumentException("Invalid catalog text length.");
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static string Required(string? value, int maximum) =>
        Optional(value, maximum) ?? throw new ArgumentException("Catalog text is required.");

    public static void Actor(Guid employeeId)
    {
        if (employeeId == Guid.Empty) throw new ArgumentException("Catalog actor is required.");
    }

    public static void Price(decimal value)
    {
        // Match numeric(18,2), without silently rounding user-entered prices.
        if (value < 0 || value > 9999999999999999.99m || decimal.Round(value, 2) != value)
            throw new ArgumentException("Invalid catalog price.");
    }
}
