namespace OptiCore.Api.Hosting;

// An explicit external origin avoids trusting client-supplied forwarded headers.
// Render terminates TLS before forwarding HTTP to the container.
public sealed record PublicOrigin(string? Value)
{
    public static PublicOrigin Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var value = configuration["Hosting:PublicOrigin"];
        if (string.IsNullOrWhiteSpace(value))
        {
            if (environment.IsStaging())
                throw new InvalidOperationException("Hosting:PublicOrigin is required in Staging.");
            return new(Value: null);
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 ||
            uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("Hosting:PublicOrigin must be an HTTPS origin without credentials, path, query or fragment.");
        return new(uri.GetLeftPart(UriPartial.Authority));
    }
}
