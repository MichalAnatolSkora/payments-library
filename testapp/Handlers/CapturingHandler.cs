using System.Net.Http.Headers;
using System.Text;

namespace Payment.Sample.Api.Handlers;

/// <summary>
/// Debug-only handler that records the last request/response pair. The Authorization
/// header is redacted so captured output never exposes the API key.
/// </summary>
public sealed class CapturingHandler : DelegatingHandler
{
    private string _reqMethod = string.Empty;
    private string _reqUrl = string.Empty;
    private string _reqHeaders = string.Empty;
    private string _reqBody = string.Empty;
    private int _statusCode;
    private string _resHeaders = string.Empty;
    private string _resBody = string.Empty;

    public CapturingHandler() : base(new HttpClientHandler()) { }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _reqMethod = request.Method.Method;
        _reqUrl = request.RequestUri?.ToString() ?? "";
        _reqHeaders = FormatHeaders(request.Headers);
        if (request.Content is not null)
        {
            _reqBody = await request.Content.ReadAsStringAsync(cancellationToken);
            request.Content = new StringContent(_reqBody,
                Encoding.UTF8,
                request.Content.Headers.ContentType?.MediaType ?? "application/json");
        }

        var response = await base.SendAsync(request, cancellationToken);
        _statusCode = (int)response.StatusCode;
        _resHeaders = FormatHeaders(response.Headers);
        _resBody = await response.Content.ReadAsStringAsync(cancellationToken);
        response.Content = new StringContent(_resBody,
            Encoding.UTF8,
            response.Content.Headers.ContentType?.MediaType ?? "application/json");
        return response;
    }

    public object Capture() => new
    {
        Request = new { Method = _reqMethod, Url = _reqUrl, Headers = _reqHeaders, Body = _reqBody },
        Response = new { StatusCode = _statusCode, Headers = _resHeaders, Body = _resBody },
    };

    private static string FormatHeaders(HttpHeaders headers)
        => string.Join("\n", headers.Select(h =>
            $"{h.Key}: {(h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ? "[redacted]" : string.Join(", ", h.Value))}"));
}
