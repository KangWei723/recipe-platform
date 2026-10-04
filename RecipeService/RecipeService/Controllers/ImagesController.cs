using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RecipeService.Domain;
using RecipeService.Dtos;
using RecipeService.Services;

namespace RecipeService.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/images")]
public class ImagesController(IImagesService service) : ControllerBase
{
    [HttpPost]
    // Kestrel-level early reject, defense-in-depth ahead of ImageFileValidator's own size check --
    // a little slack over the real limit for multipart boundary/header overhead.
    [RequestSizeLimit(ImageFileValidator.MaxSizeBytes + 4096)]
    public async Task<ActionResult<ImageUploadResponse>> Upload(IFormFile file, CancellationToken cancellationToken) =>
        Ok(await service.UploadAsync(file, cancellationToken));
}
