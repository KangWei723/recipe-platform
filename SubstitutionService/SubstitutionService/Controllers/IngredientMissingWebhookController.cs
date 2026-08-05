using System.Diagnostics;
using System.Text.Json;
using Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace SubstitutionService.Controllers;

// Consumer side of the decoupled ingredient.missing flow: QStash delivers here independently
// of SourcingService's copy of the same event (see SourcingService.Controllers for that side).
// QStash calls this directly (no user bearer token) and authenticates via its own
// Upstash-Signature HMAC check below, so it's exempted from the platform-wide JWT requirement.
[ApiController]
[AllowAnonymous]
[Route("events")]
public class IngredientMissingWebhookController(
    IOptions<QStashOptions> qstashOptions,
    ILogger<IngredientMissingWebhookController> logger) : ControllerBase
{
    [HttpPost("ingredient-missing")]
    public async Task<IActionResult> Handle()
    {
        using var bodyStream = new MemoryStream();
        await Request.Body.CopyToAsync(bodyStream);
        var rawBody = bodyStream.ToArray();

        if (!Request.Headers.TryGetValue("Upstash-Signature", out var signature) ||
            !QStashSignatureVerifier.Verify(signature.ToString(), rawBody, qstashOptions.Value.WebhookUrl, qstashOptions.Value))
        {
            return Unauthorized();
        }

        // ASP.NET Core's own request activity already picked up the inbound `traceparent`
        // header as its parent, so this child span nests under the publisher's trace for free.
        using var activity = QStashInstrumentation.ActivitySource.StartActivity(
            "ingredient-missing receive", ActivityKind.Consumer);
        activity?.SetTag("messaging.system", "qstash");
        activity?.SetTag("messaging.destination.name", QStashTopics.IngredientMissing);

        var evt = JsonSerializer.Deserialize<IngredientMissingEvent>(rawBody, QStashJson.Options);
        if (evt is null)
        {
            return BadRequest();
        }

        activity?.SetTag("ingredient.id", evt.IngredientId);

        logger.LogInformation(
            "would suggest substitutes for {IngredientName} (ingredientId={IngredientId}, recipeId={RecipeId}, userId={UserId})",
            evt.IngredientName, evt.IngredientId, evt.RecipeId, evt.UserId);

        return Ok();
    }
}
