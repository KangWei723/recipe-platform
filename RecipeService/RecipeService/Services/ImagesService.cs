using Microsoft.AspNetCore.Http;
using RecipeService.Domain;
using RecipeService.Dtos;

namespace RecipeService.Services;

public class ImagesService(IImageUploadService imageUploadService) : IImagesService
{
    public async Task<ImageUploadResponse> UploadAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ImageFileValidator.EnsureValidSize(file.Length);

        await using var stream = file.OpenReadStream();
        var header = new byte[ImageFileValidator.SignatureLengthBytes];
        var bytesRead = await stream.ReadAsync(header, 0, header.Length, cancellationToken);
        ImageFileValidator.EnsureValidSignature(header.AsSpan(0, bytesRead));

        // Rewind -- the signature check just consumed the stream's first
        // ImageFileValidator.SignatureLengthBytes bytes, and Cloudinary needs the whole file from
        // the start or it would receive one missing its header.
        stream.Position = 0;

        var url = await imageUploadService.UploadAsync(stream, file.FileName, cancellationToken);
        return new ImageUploadResponse(url);
    }
}
