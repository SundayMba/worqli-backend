using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Payments;
using Servika.Contracts.Payments;

namespace Servika.Api.Controllers;

/// <summary>Transaction fees: the schedule and a quote for an amount. The apps read
/// these instead of computing fees themselves, so what they show is what is charged.</summary>
[ApiController]
[Route("api/v1/fees")]
[Produces("application/json")]
[Tags("Fees")]
public sealed class FeesController : ControllerBase
{
    /// <summary>The fee schedule: when users start paying fees and the rates.</summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(FeeScheduleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FeeScheduleDto>> Schedule([FromServices] FeeQuoteHandler handler, CancellationToken ct) =>
        Ok(await handler.ScheduleAsync(ct));

    /// <summary>The service fee and transfer charge for an amount, as they stand today.</summary>
    /// <response code="400">Amount out of range.</response>
    [Authorize]
    [HttpGet("quote")]
    [ProducesResponseType(typeof(FeeQuoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeeQuoteDto>> Quote([FromQuery] int amount, [FromServices] FeeQuoteHandler handler, CancellationToken ct) =>
        Ok(await handler.QuoteAsync(amount, ct));
}
