using System.Net;
using System.Text.Json;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Middleware;

public class ExceptionHandlingMiddleware
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // SettlementBlockedException returns a structured list of blockers so the frontend can display them.
        if (exception is Exceptions.SettlementBlockedException sbe)
        {
            _logger.LogWarning("Settlement confirm blocked: {Count} blocker(s).", sbe.Blockers.Count);
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            var blocked = JsonSerializer.Serialize(
                new { code = "SETTLEMENT_BLOCKED", blockers = sbe.Blockers },
                options);
            return context.Response.WriteAsync(blocked);
        }

        // BreakTooShortException gets a structured payload so the frontend can render a countdown.
        if (exception is Exceptions.BreakTooShortException bts)
        {
            _logger.LogWarning("Break too short: required {Required} min, elapsed {Elapsed} min",
                bts.RequiredMinutes, bts.ElapsedMinutes);
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            var structured = JsonSerializer.Serialize(
                new { code = "BREAK_TOO_SHORT", requiredMinutes = bts.RequiredMinutes, elapsedMinutes = bts.ElapsedMinutes },
                options);
            return context.Response.WriteAsync(structured);
        }

        HttpStatusCode statusCode;
        string message;

        switch (exception)
        {
            case ResourceNotFoundException ex:
                _logger.LogWarning("Resource not found: {Message}", ex.Message);
                statusCode = HttpStatusCode.NotFound;
                message = "The requested resource was not found.";
                break;
            case InsufficientVacationBalanceException ex:
                _logger.LogWarning("Insufficient vacation balance: {Message}", ex.Message);
                statusCode = HttpStatusCode.UnprocessableEntity;
                message = ex.Message;
                break;
            case InvalidVacationAmountException ex:
                _logger.LogWarning("Invalid vacation amount: {Message}", ex.Message);
                statusCode = HttpStatusCode.BadRequest;
                message = ex.Message;
                break;
            case Exceptions.ValidationException ex:
                _logger.LogWarning("Validation error: {Message}", ex.Message);
                statusCode = HttpStatusCode.BadRequest;
                message = ex.Message;
                break;
            default:
                _logger.LogError(exception, "Unhandled exception processing request {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                statusCode = HttpStatusCode.InternalServerError;
                message = "An unexpected error occurred.";
                break;
        }

        var payload = JsonSerializer.Serialize(
            new ErrorResponseDto { Message = message },
            options);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsync(payload);
    }
}
