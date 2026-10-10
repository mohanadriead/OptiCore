using OptiCore.Application.Products;
using OptiCore.Domain.Products;

namespace OptiCore.Tests.Products;

public sealed class CatalogTests
{
    private readonly Guid actor = Guid.NewGuid();
    private readonly FakeCatalogRepository repository = new();
    private CatalogService Service => new(repository);
    private static SaveProductRequest Request(string? barcode = null) => new("Frames", 100m, Barcode: barcode);

    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    [InlineData(" 0012 ", "0012")]
    [InlineData(" 00A-12 ", "00A-12")]
    public async Task CreatesProductWithNormalizedOptionalBarcodeAndAudit(string? input, string? expected)
    {
        var p = await Service.CreateProductAsync(Request(input), actor, default);
        Assert.Equal(expected, p.Barcode);
        Assert.Equal(actor, p.CreatedByEmployeeId);
        Assert.Null(p.UpdatedAtUtc);
        Assert.True(p.IsActive);
        Assert.Null(p.PromoPrice);
        Assert.Equal(7, p.Id.Version);
        Assert.True(p.ProductNumber > 0);
        Assert.Equal(p.Id, (await Service.GetProductAsync(p.ProductNumber, default)).Id);
    }

    [Fact]
    public async Task NumberIsGeneratedAndOptionalBarcodesAllowMultipleProducts()
    {
        var first = await Service.CreateProductAsync(Request(), actor, default);
        var second = await Service.CreateProductAsync(Request(), actor, default);
        Assert.NotEqual(first.ProductNumber, second.ProductNumber);
        Assert.DoesNotContain(typeof(SaveProductRequest).GetProperties(), p => p.Name == "ProductNumber");
    }

    [Fact]
    public async Task DuplicateBarcodeRejectedOnCreateAndEditIncludingInactiveProduct()
    {
        var p = await Service.CreateProductAsync(Request("001AB"), actor, default);
        await Service.SetProductActiveAsync(p.ProductNumber, false, actor, default);
        await Assert.ThrowsAsync<DuplicateBarcodeException>(() => Service.CreateProductAsync(Request(" 001AB "), actor, default));
        var second = await Service.CreateProductAsync(Request("other"), actor, default);
        await Assert.ThrowsAsync<DuplicateBarcodeException>(() => Service.UpdateProductAsync(second.ProductNumber, Request("001AB"), actor, default));
        Assert.Equal("other", repository.Products[1].Barcode);
        await Service.UpdateProductAsync(p.ProductNumber, Request("001AB"), actor, default);
    }

    [Theory]
    [InlineData("Frames")][InlineData("Sunglasses")][InlineData("Lenses")][InlineData("ContactLenses")]
    [InlineData("Accessories")][InlineData("CleaningProducts")][InlineData("Cases")][InlineData("Other")]
    public async Task CategoryRoundtrip(string category)
    {
        var p = await Service.CreateProductAsync(Request() with { Category = category }, actor, default);
        Assert.Equal(category, (await Service.GetProductAsync(p.ProductNumber, default)).Category);
    }

    [Theory]
    [InlineData("0")][InlineData("frames")][InlineData("Unknown")][InlineData(null)]
    public async Task InvalidCategoryRejected(string? category) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateProductAsync(Request() with { Category = category! }, actor, default));

    [Fact]
    public async Task BrandNameUniqueCaseInsensitivelyEvenWhenInactive()
    {
        var b = await Service.CreateBrandAsync(new(" Ray-Ban "), actor, default);
        Assert.Equal("Ray-Ban", b.Name);
        Assert.Equal("RAY-BAN", repository.Brands.Single().NormalizedName);
        Assert.Equal(actor, b.CreatedByEmployeeId);
        foreach (var name in new[] { "ray-ban", "RAY-BAN", " Ray-Ban " })
            await Assert.ThrowsAsync<DuplicateBrandNameException>(() => Service.CreateBrandAsync(new(name), actor, default));
        await Service.SetBrandActiveAsync(b.Id, false, actor, default);
        await Assert.ThrowsAsync<DuplicateBrandNameException>(() => Service.CreateBrandAsync(new("ray-ban"), actor, default));
        Assert.Empty(await Service.ListBrandsAsync(false, default));
        Assert.Single(await Service.ListBrandsAsync(true, default));
        var renamed = await Service.UpdateBrandAsync(b.Id, new("Renamed"), actor, default);
        Assert.Equal(actor, renamed.UpdatedByEmployeeId);
        await Service.SetBrandActiveAsync(b.Id, true, actor, default);
        Assert.Single(await Service.ListBrandsAsync(false, default));
    }

    [Fact]
    public async Task InactiveBrandPreservedForExistingProductButCannotBeNewlyAssigned()
    {
        var b = await Service.CreateBrandAsync(new("Brand"), actor, default);
        var request = Request() with { BrandId = b.Id };
        var p = await Service.CreateProductAsync(request, actor, default);
        Assert.Equal(b.Id, p.BrandId); Assert.Equal("Brand", p.BrandName);
        await Service.SetBrandActiveAsync(b.Id, false, actor, default);
        var edited = await Service.UpdateProductAsync(p.ProductNumber, request with { Model = "Changed" }, actor, default);
        Assert.Equal(b.Id, edited.BrandId); Assert.False(edited.BrandIsActive);
        await Assert.ThrowsAsync<InactiveBrandException>(() => Service.CreateProductAsync(request, actor, default));
        var other = await Service.CreateProductAsync(Request(), actor, default);
        await Assert.ThrowsAsync<InactiveBrandException>(() => Service.UpdateProductAsync(other.ProductNumber, request, actor, default));
        await Assert.ThrowsAsync<BrandNotFoundException>(() => Service.CreateProductAsync(request with { BrandId = Guid.NewGuid() }, actor, default));
        Assert.Null((await Service.UpdateProductAsync(p.ProductNumber, Request(), actor, default)).BrandId);
    }

    [Theory]
    [InlineData(-1, null)][InlineData(100, -1d)]
    [InlineData(1.001, null)][InlineData(10, 1.001)]
    public async Task InvalidMoneyRejected(double regular, double? promo) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateProductAsync(Request() with
        { RegularSalePrice = (decimal)regular, PromoPrice = (decimal?)promo }, actor, default));

    [Theory]
    [InlineData(null)][InlineData(0d)][InlineData(50d)][InlineData(100d)]
    public async Task OptionalLowerOrEqualPromotionAllowed(double? promo)
    {
        var request = Request() with { PromoPrice = (decimal?)promo };
        var p = await Service.CreateProductAsync(request, actor, default);
        Assert.Equal((decimal?)promo, p.PromoPrice);
        Assert.Equal((decimal?)promo, (await Service.UpdateProductAsync(p.ProductNumber, request, actor, default)).PromoPrice);
    }

    [Fact]
    public void DomainRejectsHigherPromoOnConstructionAndUpdateWithoutMutating()
    {
        Assert.Throws<ArgumentException>(() => new Product(ProductCategory.Frames, 100, actor, promoPrice: 100.01m));
        var p = new Product(ProductCategory.Frames, 100, actor, promoPrice: 100);
        Assert.Throws<ArgumentException>(() => p.Update(ProductCategory.Other, 99.99m, actor,
            null, null, "Must not persist", null, null, null, null, 100, []));
        Assert.Equal(100, p.RegularSalePrice);
        Assert.Equal(100, p.PromoPrice);
        Assert.Equal(ProductCategory.Frames, p.Category);
        Assert.Null(p.Model);
        Assert.Null(p.UpdatedAtUtc);
        var zero = new Product(ProductCategory.Other, 0, actor, promoPrice: 0);
        Assert.Equal(0, zero.PromoPrice);
    }

    [Fact]
    public async Task ServiceRejectsHigherPromoOnCreateAndUpdate()
    {
        var invalid = Request() with { PromoPrice = 100.01m };
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateProductAsync(invalid, actor, default));
        Assert.Empty(repository.Products);
        var p = await Service.CreateProductAsync(Request(), actor, default);
        await Assert.ThrowsAsync<ArgumentException>(() => Service.UpdateProductAsync(p.ProductNumber, invalid, actor, default));
        Assert.Null(repository.Products.Single().PromoPrice);
        Assert.Null(repository.Products.Single().UpdatedAtUtc);
    }

    [Fact]
    public async Task AttributeCreateEditRemoveAndAtomicValidation()
    {
        var p = await Service.CreateProductAsync(Request() with { Attributes = [new(" Finish ", " Matte "), new("Label", "Value")] }, actor, default);
        Assert.Contains(p.Attributes, a => a.Key == "Finish" && a.Value == "Matte");
        var edited = await Service.UpdateProductAsync(p.ProductNumber, Request() with { Attributes = [new("Finish", "Gloss"), new("New", "Value")] }, actor, default);
        Assert.DoesNotContain(edited.Attributes, a => a.Key == "Label");
        Assert.Contains(edited.Attributes, a => a.Key == "Finish" && a.Value == "Gloss");
        await Assert.ThrowsAsync<ArgumentException>(() => Service.UpdateProductAsync(p.ProductNumber,
            Request() with { Model = "Must not persist", Attributes = [new("A", "1"), new(" A ", "2")] }, actor, default));
        Assert.Null(repository.Products[0].Model);
        Assert.Equal(2, repository.Products[0].Attributes.Count);
        Assert.Empty((await Service.UpdateProductAsync(p.ProductNumber, Request(), actor, default)).Attributes);
    }

    [Fact]
    public async Task LengthsAndEmptyAttributesRejected()
    {
        foreach (var request in new[] { Request(new string('x', 101)), Request() with { Model = new string('x', 201) },
            Request() with { Color = new string('x', 101) }, Request() with { Size = new string('x', 101) },
            Request() with { LensType = new string('x', 201) }, Request() with { Attributes = [new("", "v")] },
            Request() with { Attributes = [new("key", " ")] }, Request() with { Attributes = [new(new string('x', 101), "v")] },
            Request() with { Attributes = [new("key", new string('x', 501))] }, Request() with { GenderCategory = "Unknown" } })
            await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateProductAsync(request, actor, default));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateBrandAsync(new(" "), actor, default));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateBrandAsync(new(new string('x', 101)), actor, default));
        Assert.Empty(repository.Products);
    }

    [Fact]
    public async Task CategoryChangesClearHiddenFieldsAndLensTypeIsSupported()
    {
        var p = await Service.CreateProductAsync(Request() with { GenderCategory = "Kids" }, actor, default);
        Assert.Equal("Kids", p.GenderCategory);
        p = await Service.UpdateProductAsync(p.ProductNumber, Request() with { Category = "Lenses", GenderCategory = "Kids", LensType = "Type" }, actor, default);
        Assert.Null(p.GenderCategory); Assert.Equal("Type", p.LensType);
        p = await Service.UpdateProductAsync(p.ProductNumber, Request() with { Category = "Other", LensType = "Stale" }, actor, default);
        Assert.Null(p.LensType);
    }

    [Fact]
    public async Task ActivationAuditRetrievalAndSearchFilters()
    {
        var b = await Service.CreateBrandAsync(new("Ray-Ban"), actor, default);
        var p = await Service.CreateProductAsync(Request("00AB") with { BrandId = b.Id, Model = "Classic", Color = "Blue" }, actor, default);
        await Service.CreateProductAsync(Request() with { Category = "Cases" }, actor, default);
        foreach (var q in new[] { p.ProductNumber.ToString(), "00AB", "ray", "lass", "blue" })
            Assert.Equal(p.Id, Assert.Single((await Service.SearchAsync(q, null, null, true, 1, 50, default)).Items).Id);
        Assert.Single((await Service.SearchAsync(null, "Frames", null, true, 1, 50, default)).Items);
        Assert.Single((await Service.SearchAsync(null, null, b.Id, true, 1, 50, default)).Items);
        var editor = Guid.NewGuid();
        var inactive = await Service.SetProductActiveAsync(p.ProductNumber, false, editor, default);
        Assert.False(inactive.IsActive); Assert.Equal(editor, inactive.UpdatedByEmployeeId); Assert.NotNull(inactive.UpdatedAtUtc);
        Assert.False((await Service.GetProductAsync(p.ProductNumber, default)).IsActive);
        Assert.Single((await Service.SearchAsync(null, null, null, true, 1, 50, default)).Items);
        Assert.Single((await Service.SearchAsync(null, null, null, false, 1, 50, default)).Items);
        Assert.Equal(2, (await Service.SearchAsync(null, null, null, null, 1, 50, default)).Total);
        Assert.True((await Service.SetProductActiveAsync(p.ProductNumber, true, editor, default)).IsActive);
        var page = await Service.SearchAsync(null, null, null, null, 2, 1, default);
        Assert.Single(page.Items); Assert.Equal(2, page.Total);
        await Assert.ThrowsAsync<ArgumentException>(() => Service.SearchAsync(null, null, null, null, 0, 50, default));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.SearchAsync(null, null, null, null, 1, 101, default));
    }
}
