namespace OptiCore.Domain.Products;

public sealed class ProductAttribute
{
    public Guid ProductId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    private ProductAttribute() { }

    public ProductAttribute(string key, string value)
    {
        Key = CatalogValidation.Required(key, 100);
        Value = CatalogValidation.Required(value, 500);
    }

    internal void Attach(Guid productId) => ProductId = productId;
    internal void SetValue(string value) => Value = value;
}
