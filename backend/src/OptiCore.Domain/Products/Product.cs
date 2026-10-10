using OptiCore.Domain.Common;

namespace OptiCore.Domain.Products;

public sealed class Product : AuditableEntity
{
    private readonly List<ProductAttribute> attributes = [];
    public int ProductNumber { get; private set; }
    public string? Barcode { get; private set; }
    public ProductCategory Category { get; private set; }
    public Guid? BrandId { get; private set; }
    public string? Model { get; private set; }
    public string? Color { get; private set; }
    public string? Size { get; private set; }
    public GenderCategory? GenderCategory { get; private set; }
    public string? LensType { get; private set; }
    public decimal RegularSalePrice { get; private set; }
    public decimal? PromoPrice { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<ProductAttribute> Attributes => attributes.AsReadOnly();
    private Product() { }

    public Product(ProductCategory category, decimal regularSalePrice, Guid employeeId,
        string? barcode = null, Guid? brandId = null, string? model = null, string? color = null,
        string? size = null, GenderCategory? genderCategory = null, string? lensType = null,
        decimal? promoPrice = null, IEnumerable<ProductAttribute>? attributes = null) : base(employeeId)
    {
        CatalogValidation.Actor(employeeId);
        Apply(category, regularSalePrice, barcode, brandId, model, color, size, genderCategory, lensType, promoPrice, attributes);
    }

    public void Update(ProductCategory category, decimal regularSalePrice, Guid employeeId,
        string? barcode, Guid? brandId, string? model, string? color, string? size,
        GenderCategory? genderCategory, string? lensType, decimal? promoPrice, IEnumerable<ProductAttribute>? attributes)
    {
        CatalogValidation.Actor(employeeId);
        Apply(category, regularSalePrice, barcode, brandId, model, color, size, genderCategory, lensType, promoPrice, attributes);
        MarkUpdated(employeeId);
    }

    private void Apply(ProductCategory category, decimal regularSalePrice,
        string? barcode, Guid? brandId, string? model, string? color, string? size,
        GenderCategory? genderCategory, string? lensType, decimal? promoPrice, IEnumerable<ProductAttribute>? values)
    {
        if (!Enum.IsDefined(category) || (genderCategory.HasValue && !Enum.IsDefined(genderCategory.Value)))
            throw new ArgumentException("Invalid catalog category.");
        if (brandId == Guid.Empty) throw new ArgumentException("Invalid brand reference.");
        CatalogValidation.Price(regularSalePrice);
        if (promoPrice.HasValue) CatalogValidation.Price(promoPrice.Value);
        if (promoPrice > regularSalePrice)
            throw new ArgumentException("Promo price cannot exceed regular price.");
        // Validate the entire replacement before changing a tracked entity.
        var nextBarcode = CatalogValidation.Optional(barcode, 100);
        var nextModel = CatalogValidation.Optional(model, 200);
        var nextColor = CatalogValidation.Optional(color, 100);
        var nextSize = CatalogValidation.Optional(size, 100);
        var nextLensType = CatalogValidation.Optional(lensType, 200);
        var nextAttributes = (values ?? []).Select(a => a is null
            ? throw new ArgumentException("Invalid product attribute.") : new ProductAttribute(a.Key, a.Value)).ToArray();
        if (nextAttributes.Select(a => a.Key).Distinct(StringComparer.Ordinal).Count() != nextAttributes.Length)
            throw new ArgumentException("Duplicate product attribute key.");
        Category = category;
        Barcode = nextBarcode;
        BrandId = brandId;
        Model = nextModel;
        Color = nextColor;
        Size = nextSize;
        GenderCategory = category is ProductCategory.Frames or ProductCategory.Sunglasses ? genderCategory : null;
        LensType = category == ProductCategory.Lenses ? nextLensType : null;
        RegularSalePrice = regularSalePrice;
        PromoPrice = promoPrice;
        // Retain tracked records for unchanged keys; remove only omitted properties.
        attributes.RemoveAll(a => !nextAttributes.Any(next => next.Key == a.Key));
        foreach (var next in nextAttributes)
        {
            var existing = attributes.SingleOrDefault(a => a.Key == next.Key);
            if (existing is not null) existing.SetValue(next.Value);
            else { next.Attach(Id); attributes.Add(next); }
        }
    }

    public void SetActive(bool active, Guid employeeId)
    {
        CatalogValidation.Actor(employeeId);
        if (IsActive == active) return;
        IsActive = active;
        MarkUpdated(employeeId);
    }
}
