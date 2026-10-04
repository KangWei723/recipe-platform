using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace RecipeService.Services;

public class CloudinaryImageUploadService(Cloudinary cloudinary) : IImageUploadService
{
    public async Task<string> UploadAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, content),
            Folder = "larder-recipes"
        };

        var result = await cloudinary.UploadAsync(uploadParams, cancellationToken);
        if (result.Error is not null)
        {
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
        }

        // Always https -- SecureUrl, not Url, so this satisfies ImageUrlValidator's https-only
        // check downstream with no special-casing.
        return result.SecureUrl.ToString();
    }
}
