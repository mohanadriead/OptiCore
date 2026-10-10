using OptiCore.Domain.Common;

namespace OptiCore.Domain.Products;

public sealed class Brand : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    private Brand() { }

    public Brand(string name, Guid employeeId) : base(employeeId)
    {
        CatalogValidation.Actor(employeeId);
        SetName(name);
    }

    public static string NormalizeName(string name) => CatalogValidation.Required(name, 100).ToUpperInvariant();

    private void SetName(string name)
    {
        var normalized = NormalizeName(name);
        if (normalized.Length > 100) throw new ArgumentException("Invalid catalog text length.");
        Name = name.Trim();
        NormalizedName = normalized;
    }

    public void Rename(string name, Guid employeeId)
    {
        CatalogValidation.Actor(employeeId);
        SetName(name);
        MarkUpdated(employeeId);
    }

    public void SetActive(bool active, Guid employeeId)
    {
        CatalogValidation.Actor(employeeId);
        if (IsActive == active) return;
        IsActive = active;
        MarkUpdated(employeeId);
    }
}
