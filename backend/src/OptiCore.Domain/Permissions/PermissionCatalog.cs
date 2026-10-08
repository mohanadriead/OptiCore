namespace OptiCore.Domain.Permissions;

public static class PermissionCatalog
{
    public const string ReceiveStock = nameof(ReceiveStock);
    public const string GiveDiscount = nameof(GiveDiscount);
    public const string ViewDailySales = nameof(ViewDailySales);
    public const string ViewProfit = nameof(ViewProfit);
    public const string ViewSupplierDetails = nameof(ViewSupplierDetails);
    public const string ViewReports = nameof(ViewReports);
    public const string ExportData = nameof(ExportData);
    public const string ViewAuditLogs = nameof(ViewAuditLogs);
    public const int MaximumCodeLength = 64;

    public static IReadOnlyList<string> Codes { get; } = Array.AsReadOnly(new[]
    {
        ReceiveStock, GiveDiscount, ViewDailySales, ViewProfit,
        ViewSupplierDetails, ViewReports, ExportData, ViewAuditLogs
    });

    public static bool IsKnown(string? code) => code is not null && Codes.Contains(code, StringComparer.Ordinal);
    public static void Validate(string? code)
    {
        if (!IsKnown(code)) throw new ArgumentException("Unknown permission code.");
    }
}
