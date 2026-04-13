using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Payment.Core.P24.Abstractions;
using Payment.Core.P24.Models;
using Payment.Core.P24.Options;
using Payment.Core.P24.Security;
using Payment.Models.Requests;
using Payment.Models.Results;

namespace Payment.Core.P24.Providers;

/// <summary>
/// Przelewy24 (P24) implementation of <see cref="IPaymentProvider"/>.
/// REST API docs: https://developers.przelewy24.pl/
/// </summary>
public sealed class P24Provider : IPaymentProvider
{
    private const string TransactionRegisterPath = "/api/v1/transaction/register";
    private const string TransactionRequestPath = "/trnRequest/{0}";

    private readonly P24Options _options;
    private readonly HttpClient _httpClient;

    public P24Provider(P24Options options, HttpClient httpClient)
    {
        _options = options;
        _httpClient = httpClient;

        _httpClient.BaseAddress ??= options.BaseAddress;

        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_options.PosId}:{_options.ApiKey}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<CreatePaymentResult> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var sign = CryptographyProvider.ComputeRegisterSign(
            _options.MerchantId, _options.CrcKey, request.SessionId, request.Amount, request.Currency);

        var body = new RegisterTransactionRequest
        {
            MerchantId = _options.MerchantId,
            PosId = _options.PosId,
            SessionId = request.SessionId,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Email = request.CustomerEmail,
            Client = request.CustomerName,
            Country = request.Country,
            Language = request.Language,
            UrlReturn = request.ReturnUrl,
            UrlStatus = request.NotifyUrl,
            Sign = sign,
        };

        var response = await _httpClient.PostAsJsonAsync(TransactionRegisterPath, body, cancellationToken);
        var result = await JsonHelper.ReadJsonOrNull<P24ApiResponse<RegisterTransactionData>>(response, cancellationToken);

        if (result?.Data is null)
        {
            return CreatePaymentResult.Fail(
                result?.Error ?? "Transaction registration failed.",
                result?.ErrorCode ?? 0);
        }

        var baseUri = new Uri(_options.BaseAddress.OriginalString);
        var path = string.Format(TransactionRequestPath, result.Data.Token);
        var redirectUrl = new Uri(baseUri, path);

        return CreatePaymentResult.Ok(redirectUrl.AbsoluteUri, result.Data.Token);
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/api/v1/transaction/by/sessionId/{Uri.EscapeDataString(sessionId)}", cancellationToken);

        var result = await JsonHelper.ReadJsonOrNull<P24ApiResponse<TransactionStatusData>>(response, cancellationToken);

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

        var expected = CryptographyProvider.ComputeWebhookSign(
            merchantId,
            _options.CrcKey,
            posId,
            notification.SessionId,
            notification.Amount,
            originAmount,
            notification.Currency,
            orderId,
            methodId,
            statement);

        return string.Equals(expected, notification.Sign, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ConfirmPaymentResult> ConfirmPaymentAsync(
        ConfirmPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.ProviderId, out var orderId))
        {
            return ConfirmPaymentResult.Fail(
                "ProviderId must be a numeric P24 orderId.",
                0);
        }

        var sign = CryptographyProvider.ComputeNotifySign(
            _options.CrcKey, request.SessionId, orderId, request.Amount, request.Currency);

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

        var response = await _httpClient.PutAsJsonAsync("/api/v1/transaction/verify", body, cancellationToken);
        var result = await JsonHelper.ReadJsonOrNull<P24ApiResponse<JsonElement>>(response, cancellationToken);

        return result?.Error is null
            ? ConfirmPaymentResult.Ok()
            : ConfirmPaymentResult.Fail(
                result.Error ?? "Verification failed.",
                result?.ErrorCode ?? 0);
    }

    public async Task<RefundResult> RefundAsync(
        RefundRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.ProviderId, out var orderId))
        {
            return RefundResult.Fail("ProviderId must be a numeric P24 orderId.", 0);
        }

        if (request.Amount is null)
        {
            return RefundResult.Fail("P24 refunds require an explicit Amount.", 0);
        }

        var currency = request.Currency ?? "PLN";
        var requestId = Guid.NewGuid().ToString();
        var refundsUuid = Guid.NewGuid().ToString();
        var sign = CryptographyProvider.ComputeRefundSign(
            _options.MerchantId, _options.CrcKey, refundsUuid, request.Amount.Value, currency);

        var body = new RefundTransactionRequest
        {
            MerchantId = _options.MerchantId,
            PosId = _options.PosId,
            RequestId = requestId,
            RefundsUuid = refundsUuid,
            Refunds =
            [
                new RefundItem
                {
                    OrderId = orderId,
                    SessionId = request.SessionId,
                    Amount = request.Amount.Value,
                    Currency = currency,
                    Description = request.Description,
                }
            ],
            Sign = sign,
        };

        var response = await _httpClient.PostAsJsonAsync("/api/v1/transaction/refund", body, cancellationToken);
        var result = await JsonHelper.ReadJsonOrNull<P24ApiResponse<JsonElement>>(response, cancellationToken);

        return result?.Error is null
            ? RefundResult.Ok()
            : RefundResult.Fail(
                result.Error ?? "Refund failed.",
                result?.ErrorCode ?? 0);
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
