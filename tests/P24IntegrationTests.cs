using Microsoft.Extensions.Configuration;
using Payment.Core.P24.Options;
using Payment.Core.P24.Providers;
using Payment.Models.Requests;
using Payment.Models.Results;
using Xunit.Abstractions;

namespace Payment.Tests;

/// <summary>
/// Integration tests against the Przelewy24 sandbox.
/// Requires appsettings.Testing.json with valid sandbox credentials; skipped when the file is absent.
/// </summary>
public sealed class P24IntegrationTests : IDisposable
{
    private readonly P24Provider _provider;
    private readonly HttpClient _httpClient = new();
    private readonly ITestOutputHelper _output;

    public P24IntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Testing.json", optional: false)
            .Build();

        var options = config.GetSection("P24").Get<P24Options>()
            ?? throw new InvalidOperationException("Missing P24 config.");

        _provider = new P24Provider(options, _httpClient);
    }

    // -------------------------------------------------------------------------
    // Test access
    // -------------------------------------------------------------------------

    [SandboxFact]
    public async Task Credentials_AreValid_SandboxAcceptsConnection()
    {
        // P24 exposes GET /api/v1/testAccess — 200 means credentials are good.
        Assert.True(await _provider.TestAccessAsync(), "Sandbox credentials rejected.");
    }

    // -------------------------------------------------------------------------
    // CreatePaymentAsync
    // -------------------------------------------------------------------------

    [SandboxFact]
    public async Task CreatePaymentAsync_ValidRequest_ReturnsRedirectUrl()
    {
        // Arrange
        var request = new CreatePaymentRequest
        {
            SessionId = $"test-{Guid.NewGuid():N}",
            Amount = 100, // 1.00 PLN
            Currency = "PLN",
            Description = "Integration test payment",
            CustomerEmail = "test@example.com",
            CustomerName = "Jan Kowalski",
            Country = "PL",
            Language = "pl",
            ReturnUrl = "https://example.com/return",
            NotifyUrl = "https://example.com/notify",
        };

        // Act
        var result = await _provider.CreatePaymentAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"Expected success. Error: {result.ErrorCode} – {result.ErrorMessage}");
        Assert.NotNull(result.RedirectUrl);
        Assert.Contains("https://sandbox.przelewy24.pl/trnRequest/", result.RedirectUrl);
        Assert.NotNull(result.PaymentToken);

        _output.WriteLine("=========================================================");
        _output.WriteLine($"Go to this URL to complete payment:\n{result.RedirectUrl}");
        _output.WriteLine("=========================================================");
    }

    [SandboxFact]
    public async Task CreatePaymentAsync_InvalidCurrency_ReturnsFail()
    {
        // Arrange
        var request = new CreatePaymentRequest
        {
            SessionId = $"test-{Guid.NewGuid():N}",
            Amount = 100, // 1.00 PLN
            Currency = "XYZ", // Unsupported currency
            Description = "Should fail",
            CustomerEmail = "test@example.com",
            Country = "PL",
            Language = "pl",
            ReturnUrl = "https://example.com/return",
        };

        // Act
        var result = await _provider.CreatePaymentAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorCode);
    }

    // -------------------------------------------------------------------------
    // GetPaymentStatusAsync
    // -------------------------------------------------------------------------

    [SandboxFact]
    public async Task GetPaymentStatus_AfterRegistration_ReturnsPendingOrUnknown()
    {
        var sessionId = $"test-{Guid.NewGuid():N}";

        // Register first so the session exists on P24's side
        var request = new CreatePaymentRequest
        {
            SessionId = sessionId,
            Amount = 100,
            Currency = "PLN",
            Description = "Status check test",
            CustomerEmail = "test@example.com",
            ReturnUrl = "https://example.com/return",
            Country = "PL",
            Language = "pl",
        };
        var createPaymentResponse = await _provider.CreatePaymentAsync(request, CancellationToken.None);

        Assert.True(
            createPaymentResponse.Success,
            $"CreatePayment failed: {createPaymentResponse.ErrorCode} – {createPaymentResponse.ErrorMessage}");

        var result = await _provider.GetPaymentStatusAsync(sessionId);

        Assert.True(result.Success, $"Status lookup failed: {result.ErrorCode} – {result.ErrorMessage}");
        Assert.Equal(sessionId, result.Status.SessionId);
        // P24 sandbox returns Amount=0 for a registered-but-unpaid transaction
        Assert.True(result.Status.Amount >= 0);
        Assert.NotNull(result.Status.Currency);
        // A registered-but-unpaid transaction has P24 status 0 ("no payment"), which maps to Pending.
        Assert.Equal(PaymentState.Pending, result.Status.State);
    }

    [SandboxFact]
    public async Task GetPaymentStatus_UnknownSession_FailsWithNotFound()
    {
        var result = await _provider.GetPaymentStatusAsync("nonexistent-session-xyz-999");

        Assert.False(result.Success);
        Assert.Equal("404", result.ErrorCode);
    }

    public void Dispose() => _httpClient.Dispose();
}
