using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auth;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Validates incoming bearer tokens against Auth0 (issuer = the tenant domain, audience =
    /// the API identifier) and requires an authenticated principal on every endpoint by default
    /// -- individual actions/controllers opt out with [AllowAnonymous] (health checks, QStash
    /// webhooks) rather than opting in with [Authorize], so a newly added endpoint is locked
    /// down unless someone deliberately opens it up.
    /// </summary>
    public static IHostApplicationBuilder AddAuth0Authentication(this IHostApplicationBuilder builder)
    {
        var auth0 = builder.Configuration.GetSection("Auth0").Get<Auth0Options>() ?? new Auth0Options();
        if (string.IsNullOrWhiteSpace(auth0.Domain) || string.IsNullOrWhiteSpace(auth0.Audience))
        {
            throw new InvalidOperationException("Auth0:Domain and Auth0:Audience must be configured");
        }

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://{auth0.Domain}/";
                options.Audience = auth0.Audience;
                // Keep the JWT's own claim names (e.g. "sub") instead of ASP.NET Core's default
                // remap to the legacy WS-Fed ClaimTypes.* URIs, so claim lookups match what
                // Auth0 actually puts in the token.
                options.MapInboundClaims = false;
            });

        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            // Auth0 puts role names in a custom claim (arrays come through JWT bearer as one
            // Claim per element, since MapInboundClaims is off above), not the .NET RoleClaimType
            // ASP.NET Core's built-in RequireRole() looks at -- hence the explicit assertion
            // instead of policy.RequireRole().
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireAssertion(context =>
                context.User.FindAll(AuthorizationPolicies.RolesClaimType)
                    .Any(claim => claim.Value == AuthorizationPolicies.AdminRole)));
        });

        return builder;
    }
}
