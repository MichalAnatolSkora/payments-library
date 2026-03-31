using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Payment.Infrastructure.P24.Providers.Przelewy24;
using Payment.Models.Requests;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = null;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<NotificationStore>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// ---------------------------------------------------------------------------
// GET /api/config  — exposes Przelewy24 options from appsettings to the SPA
// ---------------------------------------------------------------------------
app.MapGet("/api/config", (IConfiguration config) =>
{
    var section = config.GetSection("Przelewy24");
    return Results.Ok(new
    {
        MerchantId = section.GetValue<int>("MerchantId"),
        PosId = section.GetValue<int>("PosId"),
        ApiKey = section.GetValue<string>("ApiKey") ?? "",
        CrcKey = section.GetValue<string>("CrcKey") ?? "",
        Sandbox = section.GetValue<bool>("Sandbox"),
    });
});

// ---------------------------------------------------------------------------
// Helper — build a provider from request headers
// ---------------------------------------------------------------------------
static Przelewy24Options OptionsFromHeaders(HttpRequest req) => new()
{
    MerchantId = int.Parse(req.Headers["X-MerchantId"].ToString()),
    PosId      = int.Parse(req.Headers["X-PosId"].ToString()),
    ApiKey     = req.Headers["X-ApiKey"].ToString(),
    CrcKey     = req.Headers["X-CrcKey"].ToString(),
    Sandbox    = req.Headers["X-Sandbox"].ToString().ToLower() is "true" or "1",
};

static Przelewy24Provider ProviderFromHeaders(HttpRequest req)
    => new(OptionsFromHeaders(req), new HttpClient());

static (Przelewy24Provider provider, CapturingHandler handler) ProviderWithCapture(HttpRequest req)
{
    var capturing = new CapturingHandler();
    var provider  = new Przelewy24Provider(OptionsFromHeaders(req), new HttpClient(capturing));
    return (provider, capturing);
}

// ---------------------------------------------------------------------------
// POST /api/create-payment
// ---------------------------------------------------------------------------
app.MapPost("/api/create-payment", async (
    HttpRequest req,
    [FromBody] CreatePaymentRequest body) =>
{
    var provider = ProviderFromHeaders(req);
    var result = await provider.CreatePaymentAsync(body);
    return Results.Ok(result);
});

// DEBUG: raw P24 response for create-payment (uses real provider + signature)
app.MapPost("/api/create-payment-raw", async (
    HttpRequest req,
    [FromBody] CreatePaymentRequest body) =>
{
    var (provider, handler) = ProviderWithCapture(req);
    await provider.CreatePaymentAsync(body);
    return Results.Ok(handler.Capture());
});

// ---------------------------------------------------------------------------
// GET /api/payment-status/{sessionId}
// ---------------------------------------------------------------------------
app.MapGet("/api/payment-status/{sessionId}", async (
    HttpRequest req,
    string sessionId) =>
{
    var provider = ProviderFromHeaders(req);
    var result = await provider.GetPaymentStatusAsync(sessionId);
    return Results.Ok(result);
});

// DEBUG: returns raw P24 API response for diagnosing status mapping issues
app.MapGet("/api/payment-status-raw/{sessionId}", async (
    HttpRequest req,
    string sessionId) =>
{
    var (provider, handler) = ProviderWithCapture(req);
    await provider.GetPaymentStatusAsync(sessionId);
    return Results.Ok(handler.Capture());
});

// ---------------------------------------------------------------------------
// POST /api/confirm-payment
// ---------------------------------------------------------------------------
app.MapPost("/api/confirm-payment", async (
    HttpRequest req,
    [FromBody] ConfirmPaymentRequest body) =>
{
    var provider = ProviderFromHeaders(req);
    var result = await provider.ConfirmPaymentAsync(body);
    return Results.Ok(result);
});

// DEBUG: raw P24 response for confirm-payment (uses real provider + signature)
app.MapPost("/api/confirm-payment-raw", async (
    HttpRequest req,
    [FromBody] ConfirmPaymentRequest body) =>
{
    var (provider, handler) = ProviderWithCapture(req);
    await provider.ConfirmPaymentAsync(body);
    return Results.Ok(handler.Capture());
});

// ---------------------------------------------------------------------------
// POST /api/validate-notification
// ---------------------------------------------------------------------------
app.MapPost("/api/validate-notification", (
    HttpRequest req,
    [FromBody] PaymentNotification body) =>
{
    var provider = ProviderFromHeaders(req);
    var valid = provider.ValidateNotification(body);
    return Results.Ok(new { valid });
});

// ---------------------------------------------------------------------------
// POST /api/refund
// ---------------------------------------------------------------------------
app.MapPost("/api/refund", async (
    HttpRequest req,
    [FromBody] RefundRequest body) =>
{
    var provider = ProviderFromHeaders(req);
    var result = await provider.RefundAsync(body);
    return Results.Ok(result);
});

// DEBUG: raw P24 refund response
app.MapPost("/api/refund-raw", async (
    HttpRequest req,
    [FromBody] RefundRequest body) =>
{
    var (provider, handler) = ProviderWithCapture(req);
    await provider.RefundAsync(body);
    return Results.Ok(handler.Capture());
});

// ---------------------------------------------------------------------------
// POST /api/notify  — P24 IPN webhook receiver
// ---------------------------------------------------------------------------
app.MapPost("/api/notify", (
    HttpRequest req,
    [FromBody] P24IpnPayload payload,
    NotificationStore store,
    IConfiguration config) =>
{
    var section = config.GetSection("Przelewy24");
    var options = new Przelewy24Options
    {
        MerchantId = section.GetValue<int>("MerchantId"),
        PosId = section.GetValue<int>("PosId"),
        ApiKey = section.GetValue<string>("ApiKey") ?? "",
        CrcKey = section.GetValue<string>("CrcKey") ?? "",
        Sandbox = section.GetValue<bool>("Sandbox"),
    };

    var provider = new Przelewy24Provider(options, new HttpClient());

    var notification = new PaymentNotification
    {
        SessionId = payload.SessionId,
        ProviderId = payload.OrderId.ToString(),
        Amount = payload.Amount,
        Currency = payload.Currency,
        Sign = payload.Sign,
        RawFields = new Dictionary<string, string>
        {
            ["merchantId"] = payload.MerchantId.ToString(),
            ["posId"] = payload.PosId.ToString(),
            ["originAmount"] = payload.OriginAmount.ToString(),
            ["methodId"] = payload.MethodId.ToString(),
            ["statement"] = payload.Statement ?? "",
        },
    };

    var valid = provider.ValidateNotification(notification);
    store.Add(payload, valid);

    // P24 expects HTTP 200 with no body on success
    return Results.Ok();
});

// ---------------------------------------------------------------------------
// GET /api/notifications  — returns last received IPN notifications
// ---------------------------------------------------------------------------
app.MapGet("/api/notifications", (NotificationStore store) =>
    Results.Ok(store.GetAll()));

app.Run();

// ---------------------------------------------------------------------------
// Supporting types
// ---------------------------------------------------------------------------
internal record P24IpnPayload(
    int MerchantId,
    int PosId,
    string SessionId,
    int Amount,
    int OriginAmount,
    string Currency,
    int OrderId,
    int MethodId,
    string Statement,
    string Sign);

internal class NotificationStore
{
    private readonly List<ReceivedNotification> _items = [];
    private readonly Lock _lock = new();

    public void Add(P24IpnPayload payload, bool valid)
    {
        var entry = new ReceivedNotification(
            ReceivedAt: DateTime.UtcNow,
            SessionId: payload.SessionId,
            OrderId: payload.OrderId,
            Amount: payload.Amount,
            Currency: payload.Currency,
            MethodId: payload.MethodId,
            Sign: payload.Sign,
            Valid: valid);

        lock (_lock)
        {
            _items.Insert(0, entry);
            if (_items.Count > 50)
            {
                _items.RemoveAt(_items.Count - 1);
            }
        }
    }

    public IReadOnlyList<ReceivedNotification> GetAll()
    {
        lock (_lock)
        {
            return [.. _items];
        }
    }
}

internal record ReceivedNotification(
    DateTime ReceivedAt,
    string SessionId,
    int OrderId,
    int Amount,
    string Currency,
    int MethodId,
    string Sign,
    bool Valid);

internal sealed class CapturingHandler : DelegatingHandler
{
    private string _reqMethod  = "";
    private string _reqUrl     = "";
    private string _reqHeaders = "";
    private string _reqBody    = "";
    private int    _statusCode;
    private string _resHeaders = "";
    private string _resBody    = "";

    public CapturingHandler() : base(new HttpClientHandler()) { }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _reqMethod  = request.Method.Method;
        _reqUrl     = request.RequestUri?.ToString() ?? "";
        _reqHeaders = FormatHeaders(request.Headers);
        if (request.Content is not null)
        {
            _reqBody = await request.Content.ReadAsStringAsync(cancellationToken);
            request.Content = new StringContent(_reqBody,
                System.Text.Encoding.UTF8,
                request.Content.Headers.ContentType?.MediaType ?? "application/json");
        }

        var response = await base.SendAsync(request, cancellationToken);
        _statusCode = (int)response.StatusCode;
        _resHeaders = FormatHeaders(response.Headers);
        _resBody    = await response.Content.ReadAsStringAsync(cancellationToken);
        response.Content = new StringContent(_resBody,
            System.Text.Encoding.UTF8,
            response.Content.Headers.ContentType?.MediaType ?? "application/json");
        return response;
    }

    public object Capture() => new
    {
        Request = new { Method = _reqMethod, Url = _reqUrl, Headers = _reqHeaders, Body = _reqBody },
        Response = new { StatusCode = _statusCode, Headers = _resHeaders, Body = _resBody },
    };

    private static string FormatHeaders(System.Net.Http.Headers.HttpHeaders headers)
        => string.Join("\n", headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
}
