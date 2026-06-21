using Microsoft.AspNetCore.Mvc;
using Servika.Application.Common;

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
                InvalidCredentialsException => (StatusCodes.Status401Unauthorized, ex.Message),
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
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails { Status = status, Title = title });
        }
    }
}
