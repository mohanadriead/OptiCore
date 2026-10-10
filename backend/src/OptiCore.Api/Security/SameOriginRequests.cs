using OptiCore.Api.Hosting;

namespace OptiCore.Api.Security;

// Cookie authentication needs CSRF protection, including on login. JSON binding plus SameSite=Lax
// and rejection of foreign browser origins preserves the existing request bodies/headers.
public sealed class SameOriginRequests(RequestDelegate next, PublicOrigin? publicOrigin = null)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments("/api") &&
            !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method))
        {
            var origin = request.Headers.Origin;
            var expectedOrigin = publicOrigin?.Value ?? request.Scheme + "://" + request.Host;
            var wrongHost = publicOrigin?.Value is string configured &&
                !string.Equals(new Uri(configured).Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
            var foreignOrigin = origin.Count > 0 && (origin.Count != 1 ||
                !Uri.TryCreate(origin[0], UriKind.Absolute, out var uri) ||
                !string.Equals(uri.GetLeftPart(UriPartial.Authority), expectedOrigin, StringComparison.OrdinalIgnoreCase));
            if (wrongHost || foreignOrigin || request.Headers["Sec-Fetch-Site"] == "cross-site")
            {
                await Results.Problem(statusCode: 403, title: "Cross-origin mutations are not allowed.").ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    }
}
