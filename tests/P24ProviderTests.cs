using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Payment.Core.P24.Models;
using Payment.Core.P24.Options;
using Payment.Core.P24.Providers;
using Payment.Core.P24.Security;
using Payment.Models.Requests;
using Payment.Models.Results;

namespace Payment.Tests;

/// <summary>
/// Unit tests for <see cref="P24Provider"/> against a stubbed HTTP layer. No sandbox needed.
/// </summary>
public sealed class P24ProviderTests
{
    private static readonly P24Options Options = new()
    {
        MerchantId = 12345,
        PosId = 12345,
        ApiKey = "api-key",
        CrcKey = "crc-key",
        IsSandbox = true,
    };

    private static (P24Provider Provider, StubHttpMessageHandler Handler) Create(
        Func<HttpRequestMessage, string?, HttpResponseMessage> respond,
        ILogger<P24Provider>? logger = null,
        HttpClient? client = null)
    {
        var handler = new StubHttpMessageHandler(respond);
        return (new P24Provider(Options, client ?? new HttpClient(handler), logger), handler);
    }

    /// <summary>Minimal logger that records rendered messages per level.</summary>
    private sealed class ListLogger : ILogger<P24Provider>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }

    // -------------------------------------------------------------- HttpClient

    [Fact]
    public void Constructor_DoesNotMutateHttpClient()
    {
        var client = new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException()));

        _ = new P24Provider(Options, client);

        Assert.Null(client.BaseAddress);
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task Requests_CarryBasicAuthPerRequest()
    {
        var (provider, handler) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"data":{"status":"success"},"responseCode":0}"""));

        await provider.ConfirmPaymentAsync(Confirm);

        var auth = handler.Calls[0].Request.Headers.Authorization;
        Assert.Equal("Basic", auth?.Scheme);
        Assert.Equal(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("12345:api-key")), auth?.Parameter);
    }

    [Fact]
    public async Task ClientBaseAddress_TakesPrecedenceOverOptions()
    {
        var handler = new StubHttpMessageHandler((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"data":{"token":"abc123"},"responseCode":0}"""));
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8089/") };
        var provider = new P24Provider(Options, client);

        var result = await provider.CreatePaymentAsync(NewCreateRequest());

        Assert.Equal("http://localhost:8089/api/v1/transaction/register", handler.Calls[0].Request.RequestUri!.ToString());
        Assert.Equal("http://localhost:8089/trnRequest/abc123", result.RedirectUrl);
    }

    [Fact]
    public async Task OptionsBaseAddress_UsedWhenClientHasNone()
    {
        var handler = new StubHttpMessageHandler((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"data":{"token":"abc123"},"responseCode":0}"""));
        var options = new P24Options
        {
            MerchantId = 1, PosId = 1, ApiKey = "k", CrcKey = "c", BaseAddress = new Uri("http://mock.local/"),
        };
        var provider = new P24Provider(options, new HttpClient(handler));

        await provider.CreatePaymentAsync(NewCreateRequest());

        Assert.Equal("http://mock.local/api/v1/transaction/register", handler.Calls[0].Request.RequestUri!.ToString());
    }

    [Fact]
    public void Options_DefaultToSandbox()
    {
        var options = new P24Options { MerchantId = 1, PosId = 1, ApiKey = "k", CrcKey = "c" };

        Assert.True(options.IsSandbox);
        Assert.Equal(P24Options.SandboxBaseAddress, options.BaseAddress);
    }

    // ------------------------------------------------------------- TestAccess

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public async Task TestAccess_ReflectsHttpStatus(HttpStatusCode status, bool expected)
    {
        var (provider, handler) = Create((_, _) => StubHttpMessageHandler.Json(status, """{"data":true,"responseCode":0}"""));

        Assert.Equal(expected, await provider.TestAccessAsync());
        Assert.EndsWith("/api/v1/testAccess", handler.Calls[0].Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task TestAccess_NetworkFailure_ReturnsFalse()
    {
        var (provider, _) = Create((_, _) => throw new HttpRequestException("down"));

        Assert.False(await provider.TestAccessAsync());
    }

    // ---------------------------------------------------------------- Logging

    [Fact]
    public async Task ApiFailure_LogsWarning()
    {
        var logger = new ListLogger();
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(
            HttpStatusCode.BadRequest, """{"error":"Invalid input data","code":400}"""), logger);

        await provider.ConfirmPaymentAsync(Confirm);

        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("verify", entry.Message);
        Assert.Contains("order-1", entry.Message);
        Assert.Contains("Invalid input data", entry.Message);
    }

    [Fact]
    public void RejectedNotification_LogsWarning()
    {
        var logger = new ListLogger();
        var (provider, _) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"), logger);

        provider.ValidateNotification(NewNotification(sign: "deadbeef"));

        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("signature mismatch", entry.Message);
    }

    [Fact]
    public async Task Success_DoesNotLogWarnings()
    {
        var logger = new ListLogger();
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"data":{"status":"success"},"responseCode":0}"""), logger);

        await provider.ConfirmPaymentAsync(Confirm);

        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    // ------------------------------------------------------- Input validation

    [Fact]
    public async Task CreatePayment_SessionIdTooLong_FailsBeforeSending()
    {
        var (provider, handler) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));
        var request = NewCreateRequest(sessionId: new string('s', 101));

        var result = await provider.CreatePaymentAsync(request);

        Assert.False(result.Success);
        Assert.Contains("SessionId", result.ErrorMessage);
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData("PL", "PLN", 0, "Amount")]
    [InlineData("PL", "PLNX", 100, "Currency")]
    [InlineData("POL", "PLN", 100, "Country")]
    public async Task CreatePayment_InvalidField_FailsBeforeSending(string country, string currency, int amount, string expected)
    {
        var (provider, handler) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));
        var request = NewCreateRequest(amount: amount, currency: currency, country: country);

        var result = await provider.CreatePaymentAsync(request);

        Assert.False(result.Success);
        Assert.Contains(expected, result.ErrorMessage);
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData("description", "Description")]
    [InlineData("email", "CustomerEmail")]
    [InlineData("name", "CustomerName")]
    [InlineData("language", "Language")]
    [InlineData("returnUrl", "ReturnUrl")]
    [InlineData("notifyUrl", "NotifyUrl")]
    public async Task CreatePayment_FieldOverP24Limit_FailsBeforeSending(string field, string expectedInMessage)
    {
        var (provider, handler) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));
        var request = new CreatePaymentRequest
        {
            SessionId = "order-1",
            Amount = 1000,
            Currency = "PLN",
            Description = field == "description" ? new string('d', 1025) : "Order 1",
            CustomerEmail = field == "email" ? new string('e', 51) : "a@b.pl",
            CustomerName = field == "name" ? new string('n', 41) : null,
            Country = "PL",
            Language = field == "language" ? "pol" : "pl",
            ReturnUrl = field == "returnUrl" ? "https://" + new string('r', 250) : "https://shop.example/return",
            NotifyUrl = field == "notifyUrl" ? "https://" + new string('n', 250) : null,
        };

        var result = await provider.CreatePaymentAsync(request);

        Assert.False(result.Success);
        Assert.Contains(expectedInMessage, result.ErrorMessage);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task CreatePayment_SendsSpecCompliantBody()
    {
        var (provider, handler) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"data":{"token":"abc123"},"responseCode":0}"""));

        await provider.CreatePaymentAsync(NewCreateRequest());

        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        using var doc = JsonDocument.Parse(body!);
        var root = doc.RootElement;
        Assert.Equal(12345, root.GetProperty("merchantId").GetInt32());
        Assert.Equal(12345, root.GetProperty("posId").GetInt32());
        Assert.Equal("order-1", root.GetProperty("sessionId").GetString());
        Assert.Equal(1000, root.GetProperty("amount").GetInt32());
        Assert.Equal("PLN", root.GetProperty("currency").GetString());
        Assert.Equal("a@b.pl", root.GetProperty("email").GetString());
        Assert.Equal("https://shop.example/return", root.GetProperty("urlReturn").GetString());
        Assert.Equal("UTF-8", root.GetProperty("encoding").GetString());
        Assert.Equal(
            CryptographyProvider.ComputeRegisterSign(12345, "crc-key", "order-1", 1000, "PLN"),
            root.GetProperty("sign").GetString());
        // Optional fields left null must be omitted, not sent as JSON null.
        Assert.False(root.TryGetProperty("client", out _));
        Assert.False(root.TryGetProperty("urlStatus", out _));
    }

    [Fact]
    public async Task GetPaymentStatus_EscapesSessionIdInPath()
    {
        var (provider, handler) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"data":{"orderId":1,"sessionId":"a/b c","status":0,"amount":0,"currency":"PLN"},"responseCode":0}"""));

        await provider.GetPaymentStatusAsync("a/b c");

        Assert.EndsWith("/api/v1/transaction/by/sessionId/a%2Fb%20c", handler.Calls[0].Request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Confirm_NonNumericProviderId_FailsBeforeSending()
    {
        var (provider, handler) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        var result = await provider.ConfirmPaymentAsync(new ConfirmPaymentRequest
        {
            SessionId = "order-1", ProviderId = "abc", Amount = 1000, Currency = "PLN",
        });

        Assert.False(result.Success);
        Assert.Contains("ProviderId", result.ErrorMessage);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Confirm_DataStatusNotSuccess_Fails()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"data":{"status":"error"},"responseCode":0}"""));

        var result = await provider.ConfirmPaymentAsync(Confirm);

        Assert.False(result.Success);
        Assert.Contains("'error'", result.ErrorMessage);
    }

    [Fact]
    public async Task Refund_NonNumericProviderId_FailsBeforeSending()
    {
        var (provider, handler) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        var result = await provider.RefundAsync(new RefundRequest { SessionId = "order-1", ProviderId = "x", Amount = 1 });

        Assert.False(result.Success);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Refund_NotifyUrlTooLong_FailsBeforeSending()
    {
        var (provider, handler) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        var result = await provider.RefundAsync(new RefundRequest
        {
            SessionId = "order-1", ProviderId = "1", Amount = 1, NotifyUrl = "https://" + new string('u', 250),
        });

        Assert.False(result.Success);
        Assert.Contains("NotifyUrl", result.ErrorMessage);
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Refund_NonPositiveAmount_FailsBeforeSending(int amount)
    {
        var (provider, handler) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        var result = await provider.RefundAsync(new RefundRequest { SessionId = "order-1", ProviderId = "1", Amount = amount });

        Assert.False(result.Success);
        Assert.Contains("greater than zero", result.ErrorMessage);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Refund_NullAmount_UnpaidTransaction_FailsWithoutRefunding()
    {
        // Registered but unpaid: P24 reports amount 0, so there is nothing to refund.
        var (provider, handler) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"data":{"orderId":1,"sessionId":"order-1","status":0,"amount":0,"currency":"PLN"},"responseCode":0}"""));

        var result = await provider.RefundAsync(new RefundRequest { SessionId = "order-1", ProviderId = "1", Amount = null });

        Assert.False(result.Success);
        Assert.Contains("no paid amount", result.ErrorMessage);
        Assert.Single(handler.Calls);
    }

    [Theory]
    [InlineData("""{"data":[],"responseCode":0}""")]
    [InlineData("""{"responseCode":0}""")]
    public async Task Refund_CreatedWithoutItems_Fails(string json)
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.Created, json));

        var result = await provider.RefundAsync(Refund);

        Assert.False(result.Success);
        Assert.Contains("no refund items", result.ErrorMessage);
        Assert.Equal("201", result.ErrorCode);
    }

    [Fact]
    public async Task GetPaymentStatus_EmptySessionId_FailsBeforeSending()
    {
        var (provider, handler) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        var result = await provider.GetPaymentStatusAsync("  ");

        Assert.False(result.Success);
        Assert.Empty(handler.Calls);
    }

    private static readonly ConfirmPaymentRequest Confirm = new()
    {
        SessionId = "order-1",
        ProviderId = "987654",
        Amount = 1000,
        Currency = "PLN",
    };

    private static readonly RefundRequest Refund = new()
    {
        SessionId = "order-1",
        ProviderId = "987654",
        Amount = 500,
        Description = "Refund",
    };

    // ---------------------------------------------------------------- Confirm

    [Fact]
    public async Task Confirm_NonJsonErrorResponse_Fails()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Html(HttpStatusCode.Unauthorized));

        var result = await provider.ConfirmPaymentAsync(Confirm);

        Assert.False(result.Success);
        Assert.Equal("401", result.ErrorCode);
        Assert.Contains("HTTP 401", result.ErrorMessage);
    }

    [Fact]
    public async Task Confirm_ServerErrorHtml_Fails()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Html(HttpStatusCode.InternalServerError));

        var result = await provider.ConfirmPaymentAsync(Confirm);

        Assert.False(result.Success);
        Assert.Equal("500", result.ErrorCode);
    }

    [Fact]
    public async Task Confirm_MalformedJson_Fails()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK, "{not json"));

        var result = await provider.ConfirmPaymentAsync(Confirm);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Confirm_ApiError_FailsWithMessageAndCode()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(
            HttpStatusCode.BadRequest, """{"error":"Invalid input data","code":400}"""));

        var result = await provider.ConfirmPaymentAsync(Confirm);

        Assert.False(result.Success);
        Assert.Equal("400", result.ErrorCode);
        Assert.StartsWith("Invalid input data", result.ErrorMessage);
    }

    [Fact]
    public async Task Confirm_Success_ReturnsOk()
    {
        var (provider, handler) = Create((_, _) => StubHttpMessageHandler.Json(
            HttpStatusCode.OK, """{"data":{"status":"success"},"responseCode":0}"""));

        var result = await provider.ConfirmPaymentAsync(Confirm);

        Assert.True(result.Success);
        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.EndsWith("/api/v1/transaction/verify", request.RequestUri!.AbsolutePath);
        Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
        using var doc = JsonDocument.Parse(body!);
        Assert.Equal(987654, doc.RootElement.GetProperty("orderId").GetInt64());
    }

    // ----------------------------------------------------------------- Refund

    [Fact]
    public async Task Refund_NonJsonErrorResponse_Fails()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Html(HttpStatusCode.Unauthorized));

        var result = await provider.RefundAsync(Refund);

        Assert.False(result.Success);
        Assert.Equal("401", result.ErrorCode);
    }

    [Fact]
    public async Task Refund_Created_AllItemsOk_ReturnsOkWithRefundsUuid()
    {
        string? sentUuid = null;
        var (provider, handler) = Create((_, body) =>
        {
            using var doc = JsonDocument.Parse(body!);
            sentUuid = doc.RootElement.GetProperty("refundsUuid").GetString();
            return StubHttpMessageHandler.Json(HttpStatusCode.Created,
                """{"data":[{"orderId":987654,"sessionId":"order-1","amount":500,"status":true,"message":"success"}],"responseCode":0}""");
        });

        var result = await provider.RefundAsync(Refund);

        Assert.True(result.Success);
        Assert.NotNull(sentUuid);
        Assert.Equal(sentUuid, result.RefundId);
        Assert.True(sentUuid!.Length <= 35, "P24 caps refundsUuid at 35 characters");

        var (_, body) = Assert.Single(handler.Calls);
        using var sent = JsonDocument.Parse(body!);
        Assert.False(sent.RootElement.TryGetProperty("sign", out _), "refund body must not carry a sign");
        var item = sent.RootElement.GetProperty("refunds")[0];
        Assert.False(item.TryGetProperty("currency", out _), "refund items have no currency field");
        Assert.Equal(500, item.GetProperty("amount").GetInt32());
        Assert.Equal("Refund", item.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Refund_Created_ItemRejected_Fails()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.Created,
            """{"data":[{"orderId":987654,"sessionId":"order-1","amount":500,"status":false,"message":"Insufficient funds available"}],"responseCode":0}"""));

        var result = await provider.RefundAsync(Refund);

        Assert.False(result.Success);
        Assert.Contains("Insufficient funds available", result.ErrorMessage);
    }

    [Fact]
    public async Task Refund_Conflict_ErrorArray_FailsWithItemMessage()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.Conflict,
            """{"error":[{"orderId":987654,"sessionId":"order-1","amount":500,"status":false,"message":"The amount of refund exceeds available amount for the transaction"}],"code":409}"""));

        var result = await provider.RefundAsync(Refund);

        Assert.False(result.Success);
        Assert.Equal("409", result.ErrorCode);
        Assert.Contains("exceeds available amount", result.ErrorMessage);
    }

    [Fact]
    public async Task Refund_NullAmount_LooksUpOriginalAmountForFullRefund()
    {
        var (provider, handler) = Create((request, _) =>
            request.Method == HttpMethod.Get
                ? StubHttpMessageHandler.Json(HttpStatusCode.OK,
                    """{"data":{"orderId":987654,"sessionId":"order-1","status":2,"amount":2500,"currency":"PLN"},"responseCode":0}""")
                : StubHttpMessageHandler.Json(HttpStatusCode.Created,
                    """{"data":[{"orderId":987654,"sessionId":"order-1","amount":2500,"status":true,"message":"success"}],"responseCode":0}"""));

        var result = await provider.RefundAsync(new RefundRequest
        {
            SessionId = "order-1",
            ProviderId = "987654",
            Amount = null,
        });

        Assert.True(result.Success);
        Assert.Equal(2, handler.Calls.Count);
        using var sent = JsonDocument.Parse(handler.Calls[1].Body!);
        Assert.Equal(2500, sent.RootElement.GetProperty("refunds")[0].GetProperty("amount").GetInt32());
    }

    [Fact]
    public async Task Refund_ExplicitRequestId_UsedForBothIdsAndReturned()
    {
        var (provider, handler) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.Created,
            """{"data":[{"orderId":987654,"sessionId":"order-1","amount":500,"status":true,"message":"success"}],"responseCode":0}"""));

        var result = await provider.RefundAsync(new RefundRequest
        {
            SessionId = "order-1", ProviderId = "987654", Amount = 500,
            RequestId = "refund-42-1", NotifyUrl = "https://shop.example/refund-status",
        });

        Assert.True(result.Success);
        Assert.Equal("refund-42-1", result.RefundId);
        using var sent = JsonDocument.Parse(handler.Calls[0].Body!);
        Assert.Equal("refund-42-1", sent.RootElement.GetProperty("requestId").GetString());
        Assert.Equal("refund-42-1", sent.RootElement.GetProperty("refundsUuid").GetString());
        Assert.Equal("https://shop.example/refund-status", sent.RootElement.GetProperty("urlStatus").GetString());
    }

    [Fact]
    public async Task Refund_DuplicateRequestId_IsRejectedByApiNotRefundedTwice()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.BadRequest,
            """{"error":"Request already exists","code":400}"""));

        var result = await provider.RefundAsync(new RefundRequest
        {
            SessionId = "order-1", ProviderId = "987654", Amount = 500, RequestId = "refund-42-1",
        });

        Assert.False(result.Success);
        Assert.StartsWith("Request already exists", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901234567890123456")] // 36 chars
    public async Task Refund_InvalidRequestId_FailsBeforeSending(string requestId)
    {
        var (provider, handler) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        var result = await provider.RefundAsync(new RefundRequest
        {
            SessionId = "order-1", ProviderId = "987654", Amount = 500, RequestId = requestId,
        });

        Assert.False(result.Success);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Refund_DescriptionTooLong_FailsBeforeSending()
    {
        var (provider, handler) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        var result = await provider.RefundAsync(new RefundRequest
        {
            SessionId = "order-1", ProviderId = "987654", Amount = 500,
            Description = new string('x', 36),
        });

        Assert.False(result.Success);
        Assert.Contains("35", result.ErrorMessage);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Refund_NullAmount_UnknownTransaction_Fails()
    {
        var (provider, handler) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.NotFound,
            """{"error":"Transaction not found","responseCode":0}"""));

        var result = await provider.RefundAsync(new RefundRequest
        {
            SessionId = "missing",
            ProviderId = "1",
            Amount = null,
        });

        Assert.False(result.Success);
        Assert.Equal("404", result.ErrorCode);
        Assert.Contains("Transaction not found", result.ErrorMessage);
        Assert.Single(handler.Calls); // status lookup only, no refund attempted
    }

    // ----------------------------------------------------------------- Status

    [Theory]
    [InlineData(0, PaymentState.Pending)]
    [InlineData(1, PaymentState.Processing)]
    [InlineData(2, PaymentState.Completed)]
    [InlineData(3, PaymentState.Refunded)]
    [InlineData(99, PaymentState.Unknown)]
    public async Task GetPaymentStatus_MapsP24StatusCodes(int p24Status, PaymentState expected)
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK,
            $$"""{"data":{"orderId":987654,"sessionId":"order-1","status":{{p24Status}},"amount":1000,"currency":"PLN","paymentMethod":154},"responseCode":0}"""));

        var result = await provider.GetPaymentStatusAsync("order-1");

        Assert.True(result.Success);
        Assert.Equal(expected, result.Status.State);
        Assert.Equal("987654", result.Status.ProviderId);
        Assert.Equal(1000, result.Status.Amount);
        Assert.Equal("154", result.Status.MethodId);
    }

    [Fact]
    public async Task GetPaymentStatus_NotFound_FailsWith404()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.NotFound,
            """{"error":"Transaction not found","responseCode":0}"""));

        var result = await provider.GetPaymentStatusAsync("missing");

        Assert.False(result.Success);
        Assert.Null(result.Status);
        Assert.Equal("404", result.ErrorCode);
        Assert.StartsWith("Transaction not found", result.ErrorMessage);
    }

    [Fact]
    public async Task GetPaymentStatus_Unauthorized_FailsWith401()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.Unauthorized,
            """{"error":"Incorrect authentication","code":401}"""));

        var result = await provider.GetPaymentStatusAsync("order-1");

        Assert.False(result.Success);
        Assert.Equal("401", result.ErrorCode);
    }

    // -------------------------------------------------------------- Transport

    [Fact]
    public async Task NetworkFailure_ReturnsFailureInsteadOfThrowing()
    {
        var (provider, _) = Create((_, _) => throw new HttpRequestException("connection refused"));

        var create = await provider.CreatePaymentAsync(NewCreateRequest());
        var status = await provider.GetPaymentStatusAsync("order-1");
        var confirm = await provider.ConfirmPaymentAsync(Confirm);
        var refund = await provider.RefundAsync(Refund);

        Assert.False(create.Success);
        Assert.False(status.Success);
        Assert.False(confirm.Success);
        Assert.False(refund.Success);
        Assert.Contains("connection refused", create.ErrorMessage);
    }

    [Fact]
    public async Task Timeout_ReturnsFailure()
    {
        var (provider, _) = Create((_, _) => throw new TaskCanceledException("timeout", new TimeoutException()));

        var result = await provider.ConfirmPaymentAsync(Confirm);

        Assert.False(result.Success);
        Assert.Contains("timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task CallerCancellation_StillThrows()
    {
        using var cts = new CancellationTokenSource();
        var (provider, _) = Create((_, _) => { cts.Cancel(); throw new TaskCanceledException(); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.ConfirmPaymentAsync(Confirm, cts.Token));
    }

    // ------------------------------------------------------ ValidateNotification

    private static P24Notification NewNotification(string? sign = null, int? merchantId = null, int? posId = null)
    {
        var mId = merchantId ?? Options.MerchantId;
        var pId = posId ?? Options.PosId;
        var expected = CryptographyProvider.ComputeWebhookSign(
            mId, Options.CrcKey, pId, "order-1", 1000, 1000, "PLN", 987654, 154, "p24-abc");
        return new P24Notification
        {
            MerchantId = mId,
            PosId = pId,
            SessionId = "order-1",
            Amount = 1000,
            OriginAmount = 1000,
            Currency = "PLN",
            OrderId = 987654,
            MethodId = 154,
            Statement = "p24-abc",
            Sign = sign ?? expected,
        };
    }

    [Fact]
    public void ValidateNotification_Typed_CorrectSign_ReturnsTrue()
    {
        var (provider, _) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        Assert.True(provider.ValidateNotification(NewNotification()));
    }

    [Fact]
    public void ValidateNotification_Typed_UppercaseSign_ReturnsTrue()
    {
        var (provider, _) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));
        var n = NewNotification();

        Assert.True(provider.ValidateNotification(NewNotification(sign: n.Sign.ToUpperInvariant())));
    }

    [Theory]
    [InlineData("deadbeef")]
    [InlineData("not-hex-at-all")]
    [InlineData("")]
    public void ValidateNotification_Typed_BadSign_ReturnsFalse(string sign)
    {
        var (provider, _) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        Assert.False(provider.ValidateNotification(NewNotification(sign: sign)));
    }

    [Fact]
    public void ValidateNotification_Typed_TamperedAmount_ReturnsFalse()
    {
        var (provider, _) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));
        var genuine = NewNotification();
        var tampered = new P24Notification
        {
            MerchantId = genuine.MerchantId, PosId = genuine.PosId, SessionId = genuine.SessionId,
            Amount = genuine.Amount + 1, OriginAmount = genuine.OriginAmount, Currency = genuine.Currency,
            OrderId = genuine.OrderId, MethodId = genuine.MethodId, Statement = genuine.Statement, Sign = genuine.Sign,
        };

        Assert.False(provider.ValidateNotification(tampered));
    }

    [Fact]
    public void ValidateNotification_Typed_ForeignMerchant_ReturnsFalse()
    {
        // Sign is valid for merchant 999 with our CRC, but it is not the configured merchant.
        var (provider, _) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        Assert.False(provider.ValidateNotification(NewNotification(merchantId: 999)));
    }

    [Fact]
    public void ValidateNotification_Generic_RoundTripsThroughRawFields()
    {
        var (provider, _) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        Assert.True(provider.ValidateNotification(NewNotification().ToPaymentNotification()));
    }

    [Theory]
    [InlineData("merchantId")]
    [InlineData("posId")]
    [InlineData("originAmount")]
    [InlineData("methodId")]
    public void ValidateNotification_Generic_MissingSignedField_ReturnsFalse(string missing)
    {
        var (provider, _) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));
        var full = NewNotification().ToPaymentNotification();
        var raw = full.RawFields.Where(kv => kv.Key != missing).ToDictionary(kv => kv.Key, kv => kv.Value);
        var stripped = new PaymentNotification
        {
            SessionId = full.SessionId, ProviderId = full.ProviderId, Amount = full.Amount,
            Currency = full.Currency, Sign = full.Sign, RawFields = raw,
        };

        Assert.False(provider.ValidateNotification(stripped));
    }

    [Fact]
    public void ValidateNotification_Generic_MissingStatement_TreatedAsEmpty()
    {
        var (provider, _) = Create((_, _) => throw new InvalidOperationException("no HTTP expected"));
        var expected = CryptographyProvider.ComputeWebhookSign(
            Options.MerchantId, Options.CrcKey, Options.PosId, "order-1", 1000, 1000, "PLN", 987654, 154, "");
        var n = new PaymentNotification
        {
            SessionId = "order-1", ProviderId = "987654", Amount = 1000, Currency = "PLN", Sign = expected,
            RawFields = new Dictionary<string, string>
            {
                ["merchantId"] = Options.MerchantId.ToString(),
                ["posId"] = Options.PosId.ToString(),
                ["originAmount"] = "1000",
                ["methodId"] = "154",
            },
        };

        Assert.True(provider.ValidateNotification(n));
    }

    // ----------------------------------------------------------------- Create

    [Fact]
    public async Task CreatePayment_Success_BuildsRedirectUrl()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"data":{"token":"abc123"},"responseCode":0}"""));

        var result = await provider.CreatePaymentAsync(NewCreateRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("https://sandbox.przelewy24.pl/trnRequest/abc123", result.RedirectUrl);
        Assert.Equal("abc123", result.PaymentToken);
    }

    [Fact]
    public async Task CreatePayment_ApiError_FailsWithHttpStatusInMessage()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.BadRequest,
            """{"error":"Invalid input data","code":400}"""));

        var result = await provider.CreatePaymentAsync(NewCreateRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("400", result.ErrorCode);
        Assert.Contains("Invalid input data", result.ErrorMessage);
        Assert.Contains("HTTP 400", result.ErrorMessage);
    }

    [Fact]
    public async Task CreatePayment_NonJsonResponse_FailsWithHttpStatusCode()
    {
        var (provider, _) = Create((_, _) => StubHttpMessageHandler.Html(HttpStatusCode.BadGateway));

        var result = await provider.CreatePaymentAsync(NewCreateRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("502", result.ErrorCode);
    }

    private static CreatePaymentRequest NewCreateRequest(
        string sessionId = "order-1", int amount = 1000, string currency = "PLN", string country = "PL") => new()
    {
        SessionId = sessionId,
        Amount = amount,
        Currency = currency,
        Description = "Order 1",
        CustomerEmail = "a@b.pl",
        Country = country,
        Language = "pl",
        ReturnUrl = "https://shop.example/return",
    };
}
