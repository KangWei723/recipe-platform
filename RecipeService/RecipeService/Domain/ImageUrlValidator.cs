using RecipeService.Exceptions;

namespace RecipeService.Domain;

// Same static-validator shape as IngredientCategories.IsValid/MeasurementUnits.IsValid -- this one
// throws directly (EnsureValid) rather than returning a bool, since there's a single call site
// per field and a specific message is more useful than a generic "unknown X" from the caller.
public static class ImageUrlValidator
{
    public const int MaxLength = 2048;

    // When provided, the URL must start with https://res.cloudinary.com/{cloudName}/ -- rejecting
    // both a different host entirely and a Cloudinary URL under someone else's cloud name (an
    // admin pasting an arbitrary Cloudinary asset URL from a different account would otherwise
    // pass a naive "contains cloudinary.com" check). null/omitted preserves the any-https-URL
    // behavior below unchanged -- this is an opt-in restriction, not a default.
    public static void EnsureValid(string? url, string? requiredCloudinaryCloudName = null)
    {
        if (string.IsNullOrEmpty(url)) return; // optional field

        if (url.Length > MaxLength)
        {
            throw new ValidationException($"Image URL exceeds maximum length of {MaxLength} characters");
        }

        // Uri.TryCreate happily parses "javascript:..." and "data:..." as absolute URIs with
        // those schemes, so checking the scheme is itself the javascript:/data: rejection -- no
        // separate blocklist needed.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ValidationException("Image URL must be an absolute https:// URL");
        }

        if (!string.IsNullOrEmpty(requiredCloudinaryCloudName))
        {
            var requiredPrefix = $"https://res.cloudinary.com/{requiredCloudinaryCloudName}/";
            if (!url.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                throw new ValidationException("Image URL must be a Cloudinary-hosted upload");
            }
        }
    }
}
