namespace OptiCore.Application.Products;

public sealed class ProductNotFoundException : Exception;
public sealed class BrandNotFoundException : Exception;
public sealed class DuplicateBarcodeException : Exception;
public sealed class DuplicateBrandNameException : Exception;
public sealed class InactiveBrandException : Exception;
