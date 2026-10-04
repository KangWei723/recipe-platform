namespace RecipeService.Services;

public interface IImageUploadService
{
    Task<string> UploadAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}
