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
            default:
                _logger.LogError(exception, "Unhandled exception processing request {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                statusCode = HttpStatusCode.InternalServerError;
                message = "An unexpected error occurred.";
                break;
        }

        var payload = JsonSerializer.Serialize(
            new ErrorResponseDto { Message = message },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsync(payload);
    }
}
