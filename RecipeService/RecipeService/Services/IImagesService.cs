using Microsoft.AspNetCore.Http;
using RecipeService.Dtos;

namespace RecipeService.Services;

public interface IImagesService
{
    Task<ImageUploadResponse> UploadAsync(IFormFile file, CancellationToken cancellationToken = default);
}
