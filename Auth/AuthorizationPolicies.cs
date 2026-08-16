namespace Auth;

// Central definitions for the "admin" role check, so every service that enforces it (and the
// Gateway, which enforces it a second time at the GraphQL layer for a clean error before the
// request ever reaches a backend service) reads the exact same claim/value out of the token.
public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string RolesClaimType = "https://recipemate.api/roles";
    public const string AdminRole = "admin";
}
