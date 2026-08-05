using Microsoft.AspNetCore.Http;

namespace Auth;

// Forwards the current inbound request's Authorization header onto an outgoing HttpClient
// call, so a downstream service sees the same bearer token this service was called with
// instead of an anonymous request. Register per typed HttpClient via
// .AddHttpMessageHandler<AuthHeaderForwardingHandler>() (needs
// builder.Services.AddHttpContextAccessor() + AddTransient<AuthHeaderForwardingHandler>()).
public class AuthHeaderForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is not null && context.Request.Headers.Authorization.Count > 0)
        {
            request.Headers.TryAddWithoutValidation(
                "Authorization", context.Request.Headers.Authorization.ToArray());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
