using Microsoft.AspNetCore.Mvc;
using Payment.Infrastructure.P24.Options;
using Payment.Infrastructure.P24.Providers.Przelewy24;
using Payment.Models.Requests;
using PaymentsLibrary.TestApp.Handlers;
using PaymentsLibrary.TestApp.Services;

namespace PaymentsLibrary.TestApp.Controllers;

[ApiController]
[Route("api")]
public class P24Controller : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly NotificationStore _store;
    private readonly ILogger<P24Controller> _logger;

    public P24Controller(IConfiguration configuration, NotificationStore store, ILogger<P24Controller> logger)
    {
        _configuration = configuration;
        _store = store;
        _logger = logger;
    }

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var section = _configuration.GetSection("Przelewy24");
        return Ok(new
        {
            MerchantId = section.GetValue<int>("MerchantId"),
            PosId = section.GetValue<int>("PosId"),
            ApiKey = section.GetValue<string>("ApiKey") ?? string.Empty,
            CrcKey = section.GetValue<string>("CrcKey") ?? string.Empty,
            Sandbox = section.GetValue<bool>("Sandbox"),
        });
    }

    [HttpPost("create-payment")]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        var provider = ProviderFromHeaders(Request);
        var result = await provider.CreatePaymentAsync(request);
        return Ok(result);
    }

    [HttpPost("create-payment-raw")]
    public async Task<IActionResult> CreatePaymentRaw([FromBody] CreatePaymentRequest request)
    {
        var (provider, handler) = ProviderWithCapture(Request);
        await provider.CreatePaymentAsync(request);
        return Ok(handler.Capture());
    }

    [HttpGet("payment-status/{sessionId}")]
    public async Task<IActionResult> GetPaymentStatus(string sessionId)
    {
        var provider = ProviderFromHeaders(Request);
        var result = await provider.GetPaymentStatusAsync(sessionId);
        return Ok(result);
    }

    [HttpGet("payment-status-raw/{sessionId}")]
    public async Task<IActionResult> GetPaymentStatusRaw(string sessionId)
    {
        var (provider, handler) = ProviderWithCapture(Request);
        await provider.GetPaymentStatusAsync(sessionId);
        return Ok(handler.Capture());
    }

    [HttpPost("confirm-payment")]
    public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
    {
        var provider = ProviderFromHeaders(Request);
        var result = await provider.ConfirmPaymentAsync(request);
        return Ok(result);
    }

    [HttpPost("confirm-payment-raw")]
    public async Task<IActionResult> ConfirmPaymentRaw([FromBody] ConfirmPaymentRequest request)
    {
        var (provider, handler) = ProviderWithCapture(Request);
        await provider.ConfirmPaymentAsync(request);
        return Ok(handler.Capture());
    }

    [HttpPost("validate-notification")]
    public IActionResult ValidateNotification([FromBody] PaymentNotification request)
    {
        var provider = ProviderFromHeaders(Request);
        var valid = provider.ValidateNotification(request);
        return Ok(new { valid });
    }

    [HttpPost("refund")]
    public async Task<IActionResult> Refund([FromBody] RefundRequest request)
    {
        var provider = ProviderFromHeaders(Request);
        var result = await provider.RefundAsync(request);
        return Ok(result);
    }

    [HttpPost("refund-raw")]
    public async Task<IActionResult> RefundRaw([FromBody] RefundRequest request)
    {
        var (provider, handler) = ProviderWithCapture(Request);
        await provider.RefundAsync(request);
        return Ok(handler.Capture());
    }

    [HttpPost("notify")]
    public IActionResult Notify([FromBody] InstantPaymentNotificationRequest payload)
    {
        _logger.LogInformation("Received P24 notification for SessionId: {SessionId}", payload.SessionId);

        var section = _configuration.GetSection("Przelewy24");
        var options = new P24Options
        {
            MerchantId = section.GetValue<int>("MerchantId"),
            PosId = section.GetValue<int>("PosId"),
            ApiKey = section.GetValue<string>("ApiKey") ?? string.Empty,
            CrcKey = section.GetValue<string>("CrcKey") ?? string.Empty,
            IsSandbox = section.GetValue<bool>("Sandbox"),
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
                ["statement"] = payload.Statement ?? string.Empty,
            },
        };

        var valid = provider.ValidateNotification(notification);
        _logger.LogInformation("P24 notification validation result: {Valid} (SessionId: {SessionId})", valid, payload.SessionId);

        _store.Add(payload, valid);

        return Ok();
    }

    [HttpGet("notifications")]
    public IActionResult GetNotifications()
    {
        return Ok(_store.GetAll());
    }

    private static P24Options OptionsFromHeaders(HttpRequest httpRequest) => new()
    {
        MerchantId = int.Parse(httpRequest.Headers["X-MerchantId"].ToString()),
        PosId = int.Parse(httpRequest.Headers["X-PosId"].ToString()),
        ApiKey = httpRequest.Headers["X-ApiKey"].ToString(),
        CrcKey = httpRequest.Headers["X-CrcKey"].ToString(),
        IsSandbox = httpRequest.Headers["X-Sandbox"].ToString().ToLower() is "true" or "1",
    };

    private static Przelewy24Provider ProviderFromHeaders(HttpRequest req)
        => new(OptionsFromHeaders(req), new HttpClient());

    private static (Przelewy24Provider provider, CapturingHandler handler) ProviderWithCapture(HttpRequest req)
    {
        var capturing = new CapturingHandler();
        var provider = new Przelewy24Provider(OptionsFromHeaders(req), new HttpClient(capturing));
        return (provider, capturing);
    }
}
