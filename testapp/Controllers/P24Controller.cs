using Microsoft.AspNetCore.Mvc;
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

    public P24Controller(IConfiguration configuration, NotificationStore store)
    {
        _configuration = configuration;
        _store = store;
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
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest body)
    {
        var provider = ProviderFromHeaders(Request);
        var result = await provider.CreatePaymentAsync(body);
        return Ok(result);
    }

    [HttpPost("create-payment-raw")]
    public async Task<IActionResult> CreatePaymentRaw([FromBody] CreatePaymentRequest body)
    {
        var (provider, handler) = ProviderWithCapture(Request);
        await provider.CreatePaymentAsync(body);
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
    public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest body)
    {
        var provider = ProviderFromHeaders(Request);
        var result = await provider.ConfirmPaymentAsync(body);
        return Ok(result);
    }

    [HttpPost("confirm-payment-raw")]
    public async Task<IActionResult> ConfirmPaymentRaw([FromBody] ConfirmPaymentRequest body)
    {
        var (provider, handler) = ProviderWithCapture(Request);
        await provider.ConfirmPaymentAsync(body);
        return Ok(handler.Capture());
    }

    [HttpPost("validate-notification")]
    public IActionResult ValidateNotification([FromBody] PaymentNotification body)
    {
        var provider = ProviderFromHeaders(Request);
        var valid = provider.ValidateNotification(body);
        return Ok(new { valid });
    }

    [HttpPost("refund")]
    public async Task<IActionResult> Refund([FromBody] RefundRequest body)
    {
        var provider = ProviderFromHeaders(Request);
        var result = await provider.RefundAsync(body);
        return Ok(result);
    }

    [HttpPost("refund-raw")]
    public async Task<IActionResult> RefundRaw([FromBody] RefundRequest body)
    {
        var (provider, handler) = ProviderWithCapture(Request);
        await provider.RefundAsync(body);
        return Ok(handler.Capture());
    }

    [HttpPost("notify")]
    public IActionResult Notify([FromBody] P24IpnPayload payload)
    {
        var section = _configuration.GetSection("Przelewy24");
        var options = new Przelewy24Options
        {
            MerchantId = section.GetValue<int>("MerchantId"),
            PosId = section.GetValue<int>("PosId"),
            ApiKey = section.GetValue<string>("ApiKey") ?? string.Empty,
            CrcKey = section.GetValue<string>("CrcKey") ?? string.Empty,
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
                ["statement"] = payload.Statement ?? string.Empty,
            },
        };

        var valid = provider.ValidateNotification(notification);
        _store.Add(payload, valid);

        return Ok();
    }

    [HttpGet("notifications")]
    public IActionResult GetNotifications()
    {
        return Ok(_store.GetAll());
    }

    private static Przelewy24Options OptionsFromHeaders(HttpRequest req) => new()
    {
        MerchantId = int.Parse(req.Headers["X-MerchantId"].ToString()),
        PosId = int.Parse(req.Headers["X-PosId"].ToString()),
        ApiKey = req.Headers["X-ApiKey"].ToString(),
        CrcKey = req.Headers["X-CrcKey"].ToString(),
        Sandbox = req.Headers["X-Sandbox"].ToString().ToLower() is "true" or "1",
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
