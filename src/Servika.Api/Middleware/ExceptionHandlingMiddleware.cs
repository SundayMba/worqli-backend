using Microsoft.AspNetCore.Mvc;
using Servika.Application.Common;
using Servika.Domain.Bookings;
using Servika.Domain.Disputes;

namespace Servika.Api.Middleware;

/// <summary>
/// Catches exceptions thrown by the use cases and translates them into clean
/// HTTP responses (RFC 7807 ProblemDetails). Keeps controllers free of
/// try/catch and gives clients a consistent error shape.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                EmailAlreadyInUseException => (StatusCodes.Status409Conflict, ex.Message),
                ConflictException => (StatusCodes.Status409Conflict, ex.Message),
                InvalidBookingStateException => (StatusCodes.Status409Conflict, ex.Message),
                InvalidDisputeStateException => (StatusCodes.Status409Conflict, ex.Message),
                InvalidWebhookSignatureException => (StatusCodes.Status401Unauthorized, ex.Message),
                InvalidCredentialsException => (StatusCodes.Status401Unauthorized, ex.Message),
                AccountSuspendedException => (StatusCodes.Status403Forbidden, ex.Message),
                PhoneVerificationRequiredException => (StatusCodes.Status403Forbidden, ex.Message),
                TrackingNotAllowedException => (StatusCodes.Status403Forbidden, ex.Message),
                InvalidRefreshTokenException => (StatusCodes.Status401Unauthorized, ex.Message),
                InvalidOtpException => (StatusCodes.Status400BadRequest, ex.Message),
                NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                TooManyRequestsException => (StatusCodes.Status429TooManyRequests, ex.Message),
                ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
            };

            if (status == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Unhandled exception processing {Path}", context.Request.Path);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails { Status = status, Title = title };
            // A distinct machine-readable code so the app can tell this 403 apart
            // from others (suspended account, RBAC) and launch the phone prompt.
            if (ex is PhoneVerificationRequiredException)
                problem.Extensions["code"] = "phone_verification_required";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
