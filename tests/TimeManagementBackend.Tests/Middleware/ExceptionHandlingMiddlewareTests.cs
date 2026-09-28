using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Middleware;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int StatusCode, string Body, string? ContentType)> InvokeWithAsync(Exception? thrown)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Method = "POST";
        context.Request.Path = "/api/worksessions/clock-in";

        var middleware = new ExceptionHandlingMiddleware(
            _ => thrown is null ? Task.CompletedTask : Task.FromException(thrown),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, body, context.Response.ContentType);
    }

    private static JsonElement Parse(string body) => JsonDocument.Parse(body).RootElement;

    [Fact]
    public async Task PassesThroughWhenNothingThrows()
    {
        var (status, body, _) = await InvokeWithAsync(null);

        Assert.Equal(200, status);
        Assert.Empty(body);
    }

    [Theory]
    [InlineData(typeof(ResourceNotFoundException), HttpStatusCode.NotFound)]
    [InlineData(typeof(InsufficientVacationBalanceException), HttpStatusCode.UnprocessableEntity)]
    [InlineData(typeof(InvalidVacationAmountException), HttpStatusCode.BadRequest)]
    [InlineData(typeof(ValidationException), HttpStatusCode.BadRequest)]
    public async Task MapsDomainExceptionsToTheirStatusCodes(Type exceptionType, HttpStatusCode expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "boom")!;

        var (status, body, _) = await InvokeWithAsync(exception);

        Assert.Equal((int)expected, status);
        Assert.NotNull(JsonSerializer.Deserialize<ErrorResponseDto>(
            body, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public async Task ValidationMessagesReachTheClientVerbatim()
    {
        // The frontend surfaces these strings directly, so they must not be genericised.
        var (_, body, _) = await InvokeWithAsync(new ValidationException("You are already clocked in."));

        Assert.Equal("You are already clocked in.", Parse(body).GetProperty("message").GetString());
    }

    [Fact]
    public async Task NotFoundMessageIsGenericised()
    {
        // ResourceNotFoundException messages can name internal ids, so they are replaced.
        var (_, body, _) = await InvokeWithAsync(new ResourceNotFoundException("User 8f2c-secret-id not found."));

        var message = Parse(body).GetProperty("message").GetString();
        Assert.Equal("The requested resource was not found.", message);
        Assert.DoesNotContain("8f2c-secret-id", message);
    }

    [Fact]
    public async Task UnexpectedExceptionsDoNotLeakTheirMessage()
    {
        var (status, body, _) = await InvokeWithAsync(
            new InvalidOperationException("Npgsql connection string: Password=hunter2"));

        Assert.Equal(500, status);
        Assert.Equal("An unexpected error occurred.", Parse(body).GetProperty("message").GetString());
        Assert.DoesNotContain("hunter2", body);
    }

    [Fact]
    public async Task BreakTooShort_ReturnsTheStructuredPayloadTheCountdownNeeds()
    {
        var (status, body, _) = await InvokeWithAsync(new BreakTooShortException(30, 12));

        var root = Parse(body);
        Assert.Equal(400, status);
        Assert.Equal("BREAK_TOO_SHORT", root.GetProperty("code").GetString());
        Assert.Equal(30, root.GetProperty("requiredMinutes").GetInt32());
        Assert.Equal(12, root.GetProperty("elapsedMinutes").GetInt32());
    }

    [Fact]
    public async Task SettlementBlocked_ReturnsEveryBlockerSoTheAdminSeesAllOfThem()
    {
        var (status, body, _) = await InvokeWithAsync(new SettlementBlockedException(
        [
            new BlockerDto { Type = "OpenSession", Description = "Session on 2026-03-02 is open." },
            new BlockerDto { Type = "PendingAdjustmentRequest", Description = "Request for 2026-03-05." },
        ]));

        var root = Parse(body);
        Assert.Equal(400, status);
        Assert.Equal("SETTLEMENT_BLOCKED", root.GetProperty("code").GetString());

        var blockers = root.GetProperty("blockers");
        Assert.Equal(2, blockers.GetArrayLength());
        Assert.Equal("OpenSession", blockers[0].GetProperty("type").GetString());
        Assert.Equal("PendingAdjustmentRequest", blockers[1].GetProperty("type").GetString());
    }

    [Fact]
    public async Task AllResponsesAreCamelCasedJson()
    {
        var (_, body, contentType) = await InvokeWithAsync(new ValidationException("nope"));

        Assert.Equal("application/json", contentType);
        Assert.Contains("\"message\"", body);
        Assert.DoesNotContain("\"Message\"", body);
    }
}
