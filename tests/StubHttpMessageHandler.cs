using System.Net;
using System.Text;

namespace Payment.Tests;

/// <summary>
/// Records every request and replies with a canned response chosen per request.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _respond;

    public List<(HttpRequestMessage Request, string? Body)> Calls { get; } = [];

    public StubHttpMessageHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Html(HttpStatusCode status, string html = "<html>error</html>") =>
        new(status) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Calls.Add((request, body));
        return _respond(request, body);
    }
}
