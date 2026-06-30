using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servika.Application.Common;
using Servika.Application.Payments;
using Servika.Contracts.Payments;

namespace Servika.Api.Controllers;

/// <summary>
/// Payment endpoints (PRD §Payments and Wallet). Customers initialize an escrow
/// payment against their booking; the gateway calls back to the (anonymous,
/// signature-verified, idempotent) webhook to settle it.
/// </summary>
[ApiController]
[Route("api/v1/payments")]
[Produces("application/json")]
[Tags("Payments")]
public sealed class PaymentsController : ControllerBase
{
    /// <summary>Start an escrow payment for one of the current customer's bookings.</summary>
    /// <remarks>
    /// The amount is taken from the booking (the call-out fee), never the client.
    /// Returns the gateway's hosted-checkout URL; the result arrives later via the
    /// webhook. Retrying while a payment is pending returns the same one.
    /// </remarks>
    /// <response code="200">Payment initialized (Pending) — open <c>authorizationUrl</c>.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">No such booking owned by this customer.</response>
    /// <response code="409">Already paid, or nothing is due yet.</response>
    [Authorize]
    [HttpPost("bookings/{id:guid}/initialize")]
    [ProducesResponseType(typeof(PaymentInitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentInitResponse>> Initialize(
        Guid id,
        [FromServices] InitializePaymentHandler handler,
        CancellationToken ct)
    {
        return Ok(await handler.HandleAsync(CurrentUserId(), id, ct));
    }

    /// <summary>Gateway webhook (no auth — verified by signature).</summary>
    /// <remarks>
    /// The raw body is HMAC-verified against the provider's signature header, then
    /// applied idempotently: an unknown reference or an already-settled payment is a
    /// no-op, so the gateway can safely retry. Always returns 200 once accepted.
    /// </remarks>
    /// <response code="200">Webhook received (or harmlessly ignored).</response>
    /// <response code="401">Signature missing or invalid.</response>
    [AllowAnonymous]
    [HttpPost("webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Webhook(
        [FromServices] HandlePaymentWebhookHandler handler,
        CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        // Paystack sends "x-paystack-signature"; keep a generic fallback too.
        var signature = Request.Headers["x-paystack-signature"].FirstOrDefault()
                        ?? Request.Headers["x-webhook-signature"].FirstOrDefault();

        await handler.HandleAsync(rawBody, signature, ct);
        return Ok(new { received = true });
    }

    private Guid CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId))
            throw new InvalidCredentialsException();
        return userId;
    }
}
