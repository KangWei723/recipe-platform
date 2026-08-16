using HotChocolate.Authorization;
using HotChocolate.Resolvers;
using Microsoft.AspNetCore.Authorization;

namespace Gateway.GraphQL;

// HotChocolate.Authorization's AddAuthorizationCore() only registers the schema plumbing for the
// [Authorize] directive -- it does not ship a default IAuthorizationHandler that actually asks
// ASP.NET Core's own IAuthorizationService/policies for an answer (unlike the old
// HotChocolate.AspNetCore.Authorization package). Without one, every request -- even fields with
// no [Authorize] at all -- fails at the schema's request-level authorization check with a bare
// "Unexpected Execution Error", since the resolved handler is missing. This bridges the two.
public class GraphQLAuthorizationHandler(
    IAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor) : HotChocolate.Authorization.IAuthorizationHandler
{
    public ValueTask<AuthorizeResult> AuthorizeAsync(
        IMiddlewareContext context, AuthorizeDirective directive, CancellationToken cancellationToken) =>
        EvaluateAsync(directive);

    public async ValueTask<AuthorizeResult> AuthorizeAsync(
        AuthorizationContext context, IReadOnlyList<AuthorizeDirective> directives, CancellationToken cancellationToken)
    {
        foreach (var directive in directives)
        {
            var result = await EvaluateAsync(directive);
            if (result != AuthorizeResult.Allowed)
            {
                return result;
            }
        }

        return AuthorizeResult.Allowed;
    }

    private async ValueTask<AuthorizeResult> EvaluateAsync(AuthorizeDirective directive)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return AuthorizeResult.NotAuthenticated;
        }

        if (string.IsNullOrEmpty(directive.Policy))
        {
            return AuthorizeResult.Allowed;
        }

        var result = await authorizationService.AuthorizeAsync(user, directive.Policy);
        return result.Succeeded ? AuthorizeResult.Allowed : AuthorizeResult.NotAllowed;
    }
}
