using System.Security.Claims;
using System.Text.Encodings.Web;
using Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RecipeService.Tests.Controllers;

// Stands in for real Auth0 JWT validation in WebApplicationFactory-based integration tests --
// attaches the admin role claim only when the caller sends X-Test-Admin: true, so a test can
// exercise the real [Authorize(Policy = AdminOnly)] pipeline (every other controller test in this
// repo constructs the controller directly instead, which bypasses authorization entirely) without
// needing a real Auth0 token.
public class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test-user") };
        if (Request.Headers.TryGetValue("X-Test-Admin", out var value) && value == "true")
        {
            claims.Add(new Claim(AuthorizationPolicies.RolesClaimType, AuthorizationPolicies.AdminRole));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
