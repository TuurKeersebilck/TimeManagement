using System.Net;

namespace TimeManagementBackend.Tests.Infrastructure;

/// <summary>
/// Answers outbound HTTP from a scripted list of responses so holiday-provider behaviour can be
/// exercised without reaching date.nager.at — a real call would make the suite depend on a third
/// party being up and on this year's holiday data never changing.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public List<string> RequestedUris { get; } = [];

    private StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => _respond = respond;

    public static StubHttpMessageHandler RespondingWithJson(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

    public static StubHttpMessageHandler RespondingWith(HttpStatusCode statusCode) =>
        new(_ => new HttpResponseMessage(statusCode));

    public static StubHttpMessageHandler Failing() =>
        new(_ => throw new HttpRequestException("The holiday provider is unreachable."));

    public HttpClient CreateClient() => new(this);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUris.Add(request.RequestUri!.ToString());
        return Task.FromResult(_respond(request));
    }
}
