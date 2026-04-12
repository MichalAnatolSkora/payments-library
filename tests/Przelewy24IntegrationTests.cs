using Microsoft.Extensions.Configuration;
using Payment.Core.P24.Models;
using Payment.Core.P24.Options;
using Payment.Core.P24.Providers.Przelewy24;
using Payment.Models.Requests;
using Xunit.Abstractions;

namespace PaymentsLibrary.Tests;

/// <summary>
/// Integration tests against the Przelewy24 sandbox.
/// Requires appsettings.Development.json with valid sandbox credentials.
/// </summary>
public sealed class Przelewy24IntegrationTests : IDisposable
{
    private readonly P24Provider _provider;
    private readonly HttpClient _httpClient = new();
    private readonly ITestOutputHelper _output;

    public Przelewy24IntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        var options = config.GetSection("Przelewy24").Get<P24Options>()
            ?? throw new InvalidOperationException("Missing Przelewy24 config.");

        _provider = new P24Provider(options, _httpClient);
    }

    // -------------------------------------------------------------------------
    // Test access
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Credentials_AreValid_SandboxAcceptsConnection()
    {
        // P24 exposes GET /api/v1/testAccess — 200 means credentials are good.
        // Use _httpClient directly (BaseAddress + auth already set by provider ctor).
        var response = await _httpClient.GetAsync("/api/v1/testAccess");
        Assert.True(response.IsSuccessStatusCode,
            $"Sandbox credentials rejected. HTTP {(int)response.StatusCode}");
    }

    // -------------------------------------------------------------------------
    // CreatePaymentAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreatePayment_ValidRequest_ReturnsRedirectUrl()
    {
        var result = await _provider.CreatePaymentAsync(new CreatePaymentRequest
        {
            SessionId = $"test-{Guid.NewGuid():N}",
            Amount = 100,   // 1.00 PLN
            Currency = "PLN",
            Description = "Integration test payment",
            CustomerEmail = "test@example.com",
            ReturnUrl = "https://example.com/return",
            NotifyUrl = "https://example.com/notify",
            CustomerName = "Jan Testowy",
            Country = "PL",
            Language = "pl",
        });

        Assert.True(result.Success, $"Expected success. Error: {result.ErrorCode} – {result.ErrorMessage}");
        Assert.NotNull(result.RedirectUrl);
        Assert.Contains("sandbox.przelewy24.pl/trnRequest/", result.RedirectUrl);
        Assert.NotNull(result.PaymentToken);

        _output.WriteLine("=========================================================");
        _output.WriteLine($"Go to this URL to complete payment: {result.RedirectUrl}");
        _output.WriteLine("=========================================================");
    }

    [Fact]
    public async Task CreatePayment_InvalidCurrency_ReturnsFail()
    {
        var result = await _provider.CreatePaymentAsync(new CreatePaymentRequest
        {
            SessionId = $"test-{Guid.NewGuid():N}",
            Amount = 100,
            Currency = "XYZ",   // unsupported currency
            Description = "Should fail",
            CustomerEmail = "test@example.com",
            ReturnUrl = "https://example.com/return",
            Country = "PL",
        });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorCode);
    }

    // -------------------------------------------------------------------------
    // GetPaymentStatusAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPaymentStatus_AfterRegistration_ReturnsPendingOrUnknown()
    {
        var sessionId = $"test-{Guid.NewGuid():N}";

        // Register first so the session exists on P24's side
        var created = await _provider.CreatePaymentAsync(new CreatePaymentRequest
        {
            SessionId = sessionId,
            Amount = 100,
            Currency = "PLN",
            Description = "Status check test",
            CustomerEmail = "test@example.com",
            ReturnUrl = "https://example.com/return",
            Country = "PL",
            Language = "pl",
        });

        Assert.True(created.Success, $"CreatePayment failed: {created.ErrorCode} – {created.ErrorMessage}");

        var status = await _provider.GetPaymentStatusAsync(sessionId);

        Assert.Equal(sessionId, status.SessionId);
        // P24 sandbox returns Amount=0 for a registered-but-unpaid transaction
        Assert.True(status.Amount >= 0);
        Assert.NotNull(status.Currency);
        // New unpaid transaction is pending
        Assert.True(
            status.State is PaymentState.Pending or PaymentState.Unknown,
            $"Unexpected state: {status.State} (raw P24 status — add to MapState if needed)");
    }

    [Fact]
    public async Task GetPaymentStatus_UnknownSession_ReturnsUnknownState()
    {
        var status = await _provider.GetPaymentStatusAsync("nonexistent-session-xyz-999");

        Assert.Equal(PaymentState.Unknown, status.State);
    }

    // -------------------------------------------------------------------------
    // ValidateNotification
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateNotification_CorrectSign_ReturnsTrue()
    {
        // Build a notification with a known-good sign to verify our SHA-384 logic.
        // We register a payment to get a real token, but since we can't trigger
        // an actual IPN in a unit context, we recompute the sign ourselves using
        // the same algorithm and verify the provider accepts it.
        var sessionId = "test-session-sign-check";
        var orderId = 12345;
        var amount = 200;
        var currency = "PLN";

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();
        var merchantId = int.Parse(config["Przelewy24:MerchantId"]!);
        var posId = int.Parse(config["Przelewy24:PosId"]!);
        var methodId = 154;
        var statement = "p24-123-abc";

        var sign = ComputeExpectedSign(merchantId, posId, sessionId, orderId, amount, amount, currency, methodId, statement);

        var notification = new PaymentNotification
        {
            SessionId = sessionId,
            ProviderId = orderId.ToString(),
            Amount = amount,
            Currency = currency,
            Sign = sign,
            RawFields = new Dictionary<string, string>
            {
                { "merchantId", merchantId.ToString() },
                { "posId", posId.ToString() },
                { "originAmount", amount.ToString() },
                { "methodId", methodId.ToString() },
                { "statement", statement }
            }
        };

        Assert.True(_provider.ValidateNotification(notification));
    }

    [Fact]
    public void ValidateNotification_WrongSign_ReturnsFalse()
    {
        var notification = new PaymentNotification
        {
            SessionId = "test-session",
            ProviderId = "12345",
            Amount = 200,
            Currency = "PLN",
            Sign = "invalidsignature",
        };

        Assert.False(_provider.ValidateNotification(notification));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Replicates the SHA-384 sign formula so we can build valid test notifications.
    /// </summary>
    private static string ComputeExpectedSign(
        int merchantId, int posId, string sessionId, int orderId, int amount,
        int originAmount, string currency, int methodId, string statement)
    {
        // Read CRC from config — same key the provider uses
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();
        var crc = config["Przelewy24:CrcKey"]!;

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            merchantId,
            posId,
            sessionId,
            amount,
            originAmount,
            currency,
            orderId,
            methodId,
            statement,
            crc,
        });

        var bytes = System.Security.Cryptography.SHA384.HashData(
            System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public void Dispose() => _httpClient.Dispose();
}
