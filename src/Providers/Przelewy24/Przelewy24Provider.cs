using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Payment.Models.Requests;
using Payment.Models.Results;
using PaymentsLibrary.Abstractions;
using PaymentsLibrary.Models;
using PaymentsLibrary.Providers.Przelewy24.Models;

namespace PaymentsLibrary.Providers.Przelewy24;

/// <summary>
/// Przelewy24 (P24) implementation of <see cref="IPaymentProvider"/>.
/// REST API docs: https://developers.przelewy24.pl/
/// </summary>
public sealed class Przelewy24Provider : IPaymentProvider
{
    private readonly Przelewy24Options _options;
    private readonly HttpClient _http;

    public Przelewy24Provider(Przelewy24Options options, HttpClient httpClient)
    {
        _options = options;
        _http = httpClient;

        _http.BaseAddress ??= new Uri(options.Sandbox
            ? "https://sandbox.przelewy24.pl"
            : "https://secure.przelewy24.pl");

        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_options.PosId}:{_options.ApiKey}"));
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
    }

    // -------------------------------------------------------------------------
    // IPaymentProvider
    // -------------------------------------------------------------------------

    public async Task<CreatePaymentResult> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var sign = ComputeRegisterSign(request.SessionId, request.Amount, request.Currency);

        var body = new RegisterTransactionRequest
        {
            MerchantId = _options.MerchantId,
            PosId = _options.PosId,
            SessionId = request.SessionId,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Email = request.CustomerEmail,
            UrlReturn = request.ReturnUrl,
            UrlStatus = request.NotifyUrl,
            Client = request.CustomerName,
            Country = request.Country ?? "PL",
            Language = request.Language,
            Sign = sign,
        };

        var response = await _http.PostAsJsonAsync("/api/v1/transaction/register", body, cancellationToken);
        var result = await ReadJsonOrNull<P24ApiResponse<RegisterTransactionData>>(response, cancellationToken);

        if (result?.Data is null)
        {
            return CreatePaymentResult.Fail(result?.Error ?? "unknown", result?.ErrorMessage ?? "Registration failed.");
        }

        var baseUrl = _options.Sandbox
            ? "https://sandbox.przelewy24.pl"
            : "https://secure.przelewy24.pl";
        var redirectUrl = $"{baseUrl}/trnRequest/{result.Data.Token}";
        return CreatePaymentResult.Ok(redirectUrl, result.Data.Token);
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync(
            $"/api/v1/transaction/by/sessionId/{Uri.EscapeDataString(sessionId)}", cancellationToken);

        var result = await ReadJsonOrNull<P24ApiResponse<TransactionStatusData>>(response, cancellationToken);

        var data = result?.Data;
        if (data is null)
        {
            return new PaymentStatus
            {
                SessionId = sessionId,
                Amount = 0,
                Currency = string.Empty,
                State = PaymentState.Unknown,
            };
        }

        return new PaymentStatus
        {
            SessionId = data.SessionId,
            ProviderId = data.OrderId.ToString(),
            Amount = data.Amount,
            Currency = data.Currency,
            State = MapState(data.Status),
            MethodId = data.MethodId?.ToString(),
        };

    }

    public bool ValidateNotification(PaymentNotification notification)
    {
        if (!long.TryParse(notification.ProviderId, out var orderId))
        {
            return false;
        }

        // P24 webhook signature requires several specific fields.
        // We attempt to extract them from RawFields, falling back to logical defaults.
        // However, for a real webhook, if RawFields lacks 'statement' or 'methodId', validation will likely fail
        // because the computed hash won't match the one Przelewy24 generated.

        var merchantId = notification.RawFields.TryGetValue("merchantId", out var mId) && int.TryParse(mId, out var parsedMId)
            ? parsedMId : _options.MerchantId;

        var posId = notification.RawFields.TryGetValue("posId", out var pId) && int.TryParse(pId, out var parsedPId)
            ? parsedPId : _options.PosId;

        var originAmount = notification.RawFields.TryGetValue("originAmount", out var oAmt) && int.TryParse(oAmt, out var parsedOAmt)
            ? parsedOAmt : notification.Amount;

        var methodId = notification.RawFields.TryGetValue("methodId", out var methId) && int.TryParse(methId, out var parsedMethId)
            ? parsedMethId : 0;

        var statement = notification.RawFields.TryGetValue("statement", out var stmt)
            ? stmt : string.Empty;

        var expected = ComputeWebhookSign(
            merchantId, posId, notification.SessionId, notification.Amount, originAmount,
            notification.Currency, orderId, methodId, statement);

        return string.Equals(expected, notification.Sign, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ConfirmPaymentResult> ConfirmPaymentAsync(
        ConfirmPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.ProviderId, out var orderId))
        {
            return ConfirmPaymentResult.Fail("invalid_provider_id", "ProviderId must be a numeric P24 orderId.");
        }

        var sign = ComputeNotifySign(request.SessionId, orderId, request.Amount, request.Currency);

        var body = new VerifyTransactionRequest
        {
            MerchantId = _options.MerchantId,
            PosId = _options.PosId,
            SessionId = request.SessionId,
            Amount = request.Amount,
            Currency = request.Currency,
            OrderId = orderId,
            Sign = sign,
        };

        var response = await _http.PutAsJsonAsync("/api/v1/transaction/verify", body, cancellationToken);
        var result = await ReadJsonOrNull<P24ApiResponse<JsonElement>>(response, cancellationToken);

        return result?.Error is null
            ? ConfirmPaymentResult.Ok()
            : ConfirmPaymentResult.Fail(result.Error, result.ErrorMessage ?? "Verification failed.");
    }

    public async Task<RefundResult> RefundAsync(
        RefundRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.ProviderId, out var orderId))
        {
            return RefundResult.Fail("invalid_provider_id", "ProviderId must be a numeric P24 orderId.");
        }

        if (request.Amount is null)
        {
            return RefundResult.Fail("amount_required", "P24 refunds require an explicit Amount.");
        }

        var body = new RefundTransactionRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            Refunds =
            [
                new RefundItem
                {
                    OrderId     = orderId,
                    SessionId   = request.SessionId,
                    Amount      = request.Amount.Value,
                    Description = request.Description,
                }
            ],
        };

        var response = await _http.PostAsJsonAsync("/api/v1/transaction/refund", body, cancellationToken);
        var result = await ReadJsonOrNull<P24ApiResponse<JsonElement>>(response, cancellationToken);

        return result?.Error is null
            ? RefundResult.Ok()
            : RefundResult.Fail(result.Error, result.ErrorMessage ?? "Refund failed.");
    }

    // -------------------------------------------------------------------------
    // Signing helpers
    // -------------------------------------------------------------------------

    /// <summary>SHA-384 sign for /transaction/register.</summary>
    private string ComputeRegisterSign(string sessionId, int amount, string currency)
    {
        var payload = JsonSerializer.Serialize(new
        {
            sessionId,
            merchantId = _options.MerchantId,
            amount,
            currency,
            crc = _options.CrcKey,
        });
        return Sha384Hex(payload);
    }

    /// <summary>SHA-384 sign for IPN validation and /transaction/verify.</summary>
    private string ComputeNotifySign(string sessionId, long orderId, int amount, string currency)
    {
        var payload = JsonSerializer.Serialize(new
        {
            sessionId,
            orderId,
            amount,
            currency,
            crc = _options.CrcKey,
        });
        return Sha384Hex(payload);
    }

    /// <summary>SHA-384 sign specifically for inbound webhook/IPN validation.</summary>
    private string ComputeWebhookSign(
        int merchantId, int posId, string sessionId, int amount, int originAmount,
        string currency, long orderId, int methodId, string statement)
    {
        var payload = JsonSerializer.Serialize(new
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
            crc = _options.CrcKey,
        });
        return Sha384Hex(payload);
    }

    private static string Sha384Hex(string input)
    {
        var bytes = SHA384.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<T?> ReadJsonOrNull<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return default;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static PaymentState MapState(int status) => status switch
    {
        1 => PaymentState.Pending,
        2 => PaymentState.Completed,
        3 => PaymentState.Cancelled,
        4 => PaymentState.Refunded,
        5 => PaymentState.Failed,
        _ => PaymentState.Unknown,
    };
}
