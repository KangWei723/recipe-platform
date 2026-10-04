namespace RecipeService.Domain;

public class CloudinaryOptions
{
    public string CloudName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";

    // Off by default -- see ImageUrlValidator.EnsureValid. Enable only after confirming no
    // existing recipe has an image_url from a different host (that recipe would otherwise fail
    // validation on its next edit).
    public bool RestrictImageUrlsToOwnCloud { get; set; } = false;
}
