using Microsoft.AspNetCore.Mvc;
using Servika.Application.Catalogue;
using Servika.Contracts.Catalogue;

namespace Servika.Api.Controllers;

/// <summary>
/// Public fixed-price service discovery: the Home rail of services customers can
/// book in one tap at the published price, and each service's showcase photo.
/// Open to guests, like the rest of the catalogue.
/// </summary>
[ApiController]
[Route("api/v1/services")]
public sealed class ServicesController : ControllerBase
{
    /// <summary>Fixed-price services across verified artisans, ranked available-first,
    /// then reputation, then proximity to the given coords.</summary>
    /// <response code="200">Up to 12 bookable services (empty until artisans publish).</response>
    [HttpGet("featured")]
    [ProducesResponseType(typeof(IReadOnlyList<FeaturedServiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeaturedServiceDto>>> Featured(
        [FromServices] GetFeaturedServicesHandler handler,
        CancellationToken ct,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null)
    {
        return Ok(await handler.HandleAsync(lat, lng, GetFeaturedServicesHandler.MaxFeatured, ct));
    }

    /// <summary>Every bookable fixed-price service, same ranking as the rail but
    /// uncapped — the "Services close to you" screen.</summary>
    /// <response code="200">All bookable services (empty until artisans publish).</response>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(IReadOnlyList<FeaturedServiceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeaturedServiceDto>>> Nearby(
        [FromServices] GetFeaturedServicesHandler handler,
        CancellationToken ct,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null)
    {
        return Ok(await handler.HandleAsync(lat, lng, null, ct));
    }

    /// <summary>One fixed-price service with its provider summary — the service profile page.</summary>
    /// <response code="200">The service.</response>
    /// <response code="404">Unknown service, or its provider is not listed.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FeaturedServiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeaturedServiceDto>> Get(
        Guid id,
        [FromServices] GetServiceHandler handler,
        CancellationToken ct,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null)
    {
        return Ok(await handler.HandleAsync(id, lat, lng, ct));
    }

    /// <summary>A service's showcase photo (image bytes).</summary>
    /// <response code="200">The photo.</response>
    /// <response code="404">Unknown service, or no photo uploaded.</response>
    [HttpGet("{id:guid}/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Photo(
        Guid id,
        [FromServices] GetServicePhotoHandler handler,
        CancellationToken ct)
    {
        var file = await handler.HandleAsync(id, ct);
        return File(file.Content, file.ContentType);
    }
}
