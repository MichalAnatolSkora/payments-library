using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Payment.Core.P24.Models;
using Payment.Core.P24.Options;
using Payment.Core.P24.Security;
using Payment.Models.Abstractions;
using Payment.Models.Requests;
using Payment.Models.Results;

namespace Payment.Core.P24.Providers;

/// <summary>
/// Przelewy24 (P24) implementation of <see cref="IPaymentProvider"/>.
/// REST API docs: https://developers.przelewy24.pl/
/// </summary>
/// <remarks>
/// The provider never mutates the <see cref="HttpClient"/> it is given: the base address comes from
/// the client when set, otherwise from <see cref="P24Options.BaseAddress"/>, and Basic auth is attached per request.
/// </remarks>
public sealed class P24Provider : IPaymentProvider
{
    private const string TestAccessPath = "/api/v1/testAccess";
    private const string TransactionRegisterPath = "/api/v1/transaction/register";
    private const string TransactionRequestPath = "/trnRequest/{0}";
    private const string TransactionBySessionIdPath = "/api/v1/transaction/by/sessionId";
    private const string TransactionVerifyPath = "/api/v1/transaction/verify";
    private const string TransactionRefundPath = "/api/v1/transaction/refund";

    // Field limits from the P24 OpenAPI spec.
    private const int MaxSessionIdLength = 100;
    private const int MaxDescriptionLength = 1024;
    private const int MaxEmailLength = 50;
    private const int MaxClientNameLength = 40;
    private const int MaxUrlLength = 250;
    private const int MaxRefundRequestIdLength = 35;
    private const int MaxRefundDescriptionLength = 35;

    private readonly P24Options _options;
    private readonly HttpClient _httpClient;
    private readonly Uri _baseAddress;
    private readonly AuthenticationHeaderValue _authorization;
    private readonly ILogger<P24Provider> _logger;

    public P24Provider(P24Options options, HttpClient httpClient, ILogger<P24Provider>? logger = null)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<P24Provider>.Instance;
        _baseAddress = httpClient.BaseAddress ?? options.BaseAddress;

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.PosId}:{_options.ApiKey}"));
        _authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    /// <summary>
    /// Calls GET /api/v1/testAccess. Returns true when the configured credentials are accepted.
    /// </summary>
    public Task<bool> TestAccessAsync(CancellationToken cancellationToken = default)
        => GuardAsync(async () =>
        {
            using var response = await SendAsync(HttpMethod.Get, TestAccessPath, null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("P24 testAccess rejected credentials for PosId {PosId} (HTTP {StatusCode}).",
                    _options.PosId, (int)response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }, (_, _) => false, cancellationToken);

    public Task<CreatePaymentResult> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
        => GuardAsync(async () =>
        {
            if (Validate(request) is { } problem)
            {
                return CreatePaymentResult.Fail(problem, 0);
            }

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

            using var response = await SendAsync(HttpMethod.Post, TransactionRegisterPath, body, cancellationToken);
            var result = await JsonHelper.ReadJsonOrNull<P24ApiResponse<RegisterTransactionData>>(response, cancellationToken);

            if (!response.IsSuccessStatusCode || result is null || result.HasError || result.Data is null)
            {
                return CreatePaymentResult.Fail(
                    FailureMessage("register", request.SessionId, response, result, "Transaction registration failed."),
                    FailureCode(response, result));
            }

            var path = string.Format(TransactionRequestPath, result.Data.Token);
            var redirectUrl = new Uri(_baseAddress, path);

            _logger.LogDebug("P24 register succeeded for session {SessionId}.", request.SessionId);
            return CreatePaymentResult.Ok(redirectUrl.AbsoluteUri, result.Data.Token);
        }, CreatePaymentResult.Fail, cancellationToken);

    public Task<PaymentStatusResult> GetPaymentStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => GuardAsync(async () =>
        {
            if (ValidateSessionId(sessionId) is { } problem)
            {
                return PaymentStatusResult.Fail(problem, 0);
            }

            using var response = await SendAsync(
                HttpMethod.Get, $"{TransactionBySessionIdPath}/{Uri.EscapeDataString(sessionId)}", null, cancellationToken);

            var result = await JsonHelper.ReadJsonOrNull<P24ApiResponse<TransactionStatusData>>(response, cancellationToken);

            if (!response.IsSuccessStatusCode || result is null || result.HasError || result.Data is null)
            {
                return PaymentStatusResult.Fail(
                    FailureMessage("status", sessionId, response, result, "Status lookup failed."),
                    FailureCode(response, result));
            }

            var data = result.Data;
            return PaymentStatusResult.Ok(new PaymentStatus
            {
                SessionId = data.SessionId,
                ProviderId = data.OrderId.ToString(),
                Amount = data.Amount,
                Currency = data.Currency,
                State = MapState(data.Status),
                MethodId = data.MethodId?.ToString(),
            });
        }, PaymentStatusResult.Fail, cancellationToken);

    /// <summary>
    /// Validates a typed P24 IPN: the merchant and POS must match this provider's configuration
    /// and the SHA-384 sign must match. Comparison is constant-time.
    /// </summary>
    public bool ValidateNotification(P24Notification notification)
    {
        if (notification.MerchantId != _options.MerchantId || notification.PosId != _options.PosId)
        {
            _logger.LogWarning(
                "P24 notification for session {SessionId} rejected: merchantId {MerchantId}/posId {PosId} do not match configuration.",
                notification.SessionId, notification.MerchantId, notification.PosId);
            return false;
        }

        var expected = CryptographyProvider.ComputeWebhookSign(
            notification.MerchantId,
            _options.CrcKey,
            notification.PosId,
            notification.SessionId,
            notification.Amount,
            notification.OriginAmount,
            notification.Currency,
            notification.OrderId,
            notification.MethodId,
            notification.Statement ?? string.Empty);

        if (!CryptographyProvider.SignsMatch(expected, notification.Sign))
        {
            _logger.LogWarning("P24 notification for session {SessionId} (orderId {OrderId}) rejected: signature mismatch.",
                notification.SessionId, notification.OrderId);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates the generic notification model. Every field P24 signs must be present in
    /// <see cref="PaymentNotification.RawFields"/> (merchantId, posId, originAmount, methodId;
    /// statement may be omitted); otherwise the notification is rejected rather than guessed at.
    /// </summary>
    public bool ValidateNotification(PaymentNotification notification)
    {
        if (!P24Notification.TryFromPaymentNotification(notification, out var typed))
        {
            _logger.LogWarning(
                "P24 notification for session {SessionId} rejected: RawFields must contain merchantId, posId, originAmount and methodId, and ProviderId must be numeric.",
                notification.SessionId);
            return false;
        }

        return ValidateNotification(typed);
    }

    public Task<ConfirmPaymentResult> ConfirmPaymentAsync(
        ConfirmPaymentRequest request,
        CancellationToken cancellationToken = default)
        => GuardAsync(async () =>
        {
            if (ValidateSessionId(request.SessionId) is { } problem)
            {
                return ConfirmPaymentResult.Fail(problem, 0);
            }

            if (!long.TryParse(request.ProviderId, out var orderId))
            {
                return ConfirmPaymentResult.Fail("ProviderId must be a numeric P24 orderId.", 0);
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

            using var response = await SendAsync(HttpMethod.Put, TransactionVerifyPath, body, cancellationToken);
            var result = await JsonHelper.ReadJsonOrNull<P24ApiResponse<VerifyTransactionData>>(response, cancellationToken);

            if (!response.IsSuccessStatusCode || result is null || result.HasError)
            {
                return ConfirmPaymentResult.Fail(
                    FailureMessage("verify", request.SessionId, response, result, "Verification failed."),
                    FailureCode(response, result));
            }

            // P24 reports "success" in data.status; anything else is a rejection despite HTTP 200.
            var status = result.Data?.Status;
            if (status is not null && !string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("P24 verify for session {SessionId} returned status '{Status}'.", request.SessionId, status);
                return ConfirmPaymentResult.Fail($"Verification returned status '{status}'.", (int)response.StatusCode);
            }

            _logger.LogDebug("P24 verify succeeded for session {SessionId}.", request.SessionId);
            return ConfirmPaymentResult.Ok();
        }, ConfirmPaymentResult.Fail, cancellationToken);

    public Task<RefundResult> RefundAsync(
        RefundRequest request,
        CancellationToken cancellationToken = default)
        => GuardAsync(async () =>
        {
            if (ValidateSessionId(request.SessionId) is { } problem)
            {
                return RefundResult.Fail(problem, 0);
            }

            if (!long.TryParse(request.ProviderId, out var orderId))
            {
                return RefundResult.Fail("ProviderId must be a numeric P24 orderId.", 0);
            }

            if (request.Description is { Length: > MaxRefundDescriptionLength })
            {
                return RefundResult.Fail(
                    $"Description must be at most {MaxRefundDescriptionLength} characters for P24 refunds.", 0);
            }

            if (request.RequestId is not null
                && (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > MaxRefundRequestIdLength))
            {
                return RefundResult.Fail(
                    $"RequestId must be 1-{MaxRefundRequestIdLength} characters for P24 refunds.", 0);
            }

            if (request.NotifyUrl is { Length: > MaxUrlLength })
            {
                return RefundResult.Fail($"NotifyUrl must be at most {MaxUrlLength} characters.", 0);
            }

            var amount = request.Amount;
            if (amount is null)
            {
                // Full refund: look up the original amount. Refunds are always in the transaction currency.
                var lookup = await GetPaymentStatusAsync(request.SessionId, cancellationToken);
                if (!lookup.Success)
                {
                    return RefundResult.Fail(
                        $"Could not determine the original amount for a full refund: {lookup.ErrorMessage}",
                        int.TryParse(lookup.ErrorCode, out var code) ? code : 0);
                }

                if (lookup.Status.Amount <= 0)
                {
                    return RefundResult.Fail(
                        "Transaction has no paid amount to refund; pass Amount explicitly.", 0);
                }

                amount = lookup.Status.Amount;
            }

            if (amount <= 0)
            {
                return RefundResult.Fail("Refund amount must be greater than zero.", 0);
            }

            // One identifier serves as both requestId and refundsUuid, so a retry with the same
            // RequestId is rejected by P24 as "Request already exists" instead of refunding twice.
            var refundId = request.RequestId ?? Guid.NewGuid().ToString("N");

            var body = new RefundTransactionRequest
            {
                RequestId = refundId,
                RefundsUuid = refundId,
                UrlStatus = request.NotifyUrl,
                Refunds =
                [
                    new RefundItem
                    {
                        OrderId = orderId,
                        SessionId = request.SessionId,
                        Amount = amount.Value,
                        Description = request.Description,
                    }
                ],
            };

            using var response = await SendAsync(HttpMethod.Post, TransactionRefundPath, body, cancellationToken);
            var result = await JsonHelper.ReadJsonOrNull<P24ApiResponse<List<RefundResultItem>>>(response, cancellationToken);

            if (!response.IsSuccessStatusCode || result is null || result.HasError)
            {
                return RefundResult.Fail(
                    FailureMessage("refund", request.SessionId, response, result, "Refund failed."),
                    FailureCode(response, result));
            }

            // HTTP 201 still carries a per-item status; a rejected item is a failed refund.
            if (result.Data is null || result.Data.Count == 0)
            {
                return RefundResult.Fail(
                    FailureMessage("refund", request.SessionId, response, result, "Refund response contained no refund items."),
                    (int)response.StatusCode);
            }

            var rejected = result.Data.Where(item => !item.Status).ToList();
            if (rejected.Count > 0)
            {
                var message = string.Join("; ", rejected.Select(item => item.Message ?? "Refund rejected."));
                _logger.LogWarning("P24 refund {RefundId} for session {SessionId} rejected: {Message}",
                    refundId, request.SessionId, message);
                return RefundResult.Fail(message, (int)response.StatusCode);
            }

            _logger.LogDebug("P24 refund {RefundId} succeeded for session {SessionId}.", refundId, request.SessionId);
            return RefundResult.Ok(refundId);
        }, RefundResult.Fail, cancellationToken);

    private static PaymentState MapState(int status) => status switch
    {
        // Per P24 docs: 0 - no payment, 1 - advance payment, 2 - payment made, 3 - payment returned.
        0 => PaymentState.Pending,
        1 => PaymentState.Processing,
        2 => PaymentState.Completed,
        3 => PaymentState.Refunded,
        _ => PaymentState.Unknown,
    };

    // ------------------------------------------------------------------ helpers

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, new Uri(_baseAddress, path));
        request.Headers.Authorization = _authorization;
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return _httpClient.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Turns transport failures into result objects so callers get one error contract.
    /// Caller-initiated cancellation still propagates as <see cref="OperationCanceledException"/>.
    /// </summary>
    private async Task<TResult> GuardAsync<TResult>(
        Func<Task<TResult>> action,
        Func<string, int, TResult> fail,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not reach Przelewy24 at {BaseAddress}.", _baseAddress);
            return fail($"Could not reach Przelewy24: {ex.Message}", (int?)ex.StatusCode ?? 0);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Request to Przelewy24 at {BaseAddress} timed out.", _baseAddress);
            return fail("Request to Przelewy24 timed out.", 0);
        }
    }

    private string FailureMessage<T>(
        string operation, string sessionId, HttpResponseMessage response, P24ApiResponse<T>? result, string fallback)
    {
        var message = result?.ErrorMessage ?? fallback;
        _logger.LogWarning("P24 {Operation} failed for session {SessionId}: {Message} (HTTP {StatusCode}).",
            operation, sessionId, message, (int)response.StatusCode);
        return $"{message} (HTTP {(int)response.StatusCode})";
    }

    private static int FailureCode<T>(HttpResponseMessage response, P24ApiResponse<T>? result)
        => result?.ErrorCode ?? (int)response.StatusCode;

    private static string? ValidateSessionId(string? sessionId)
        => string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > MaxSessionIdLength
            ? $"SessionId must be 1-{MaxSessionIdLength} characters."
            : null;

    /// <summary>
    /// Client-side check of the limits P24 enforces on /transaction/register, so callers get
    /// a precise message instead of a generic HTTP 400.
    /// </summary>
    private static string? Validate(CreatePaymentRequest r)
    {
        if (ValidateSessionId(r.SessionId) is { } sessionProblem)
        {
            return sessionProblem;
        }

        if (r.Amount <= 0)
        {
            return "Amount must be greater than zero (minor currency units).";
        }

        if (r.Currency is not { Length: 3 })
        {
            return "Currency must be a 3-letter ISO code, e.g. PLN.";
        }

        if (string.IsNullOrWhiteSpace(r.Description) || r.Description.Length > MaxDescriptionLength)
        {
            return $"Description must be 1-{MaxDescriptionLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(r.CustomerEmail) || r.CustomerEmail.Length > MaxEmailLength)
        {
            return $"CustomerEmail must be 1-{MaxEmailLength} characters.";
        }

        if (r.CustomerName is { Length: > MaxClientNameLength })
        {
            return $"CustomerName must be at most {MaxClientNameLength} characters.";
        }

        if (r.Country is not { Length: 2 })
        {
            return "Country must be a 2-letter ISO code, e.g. PL.";
        }

        if (r.Language is not { Length: 2 })
        {
            return "Language must be a 2-letter ISO 639-1 code, e.g. pl.";
        }

        if (string.IsNullOrWhiteSpace(r.ReturnUrl) || r.ReturnUrl.Length > MaxUrlLength)
        {
            return $"ReturnUrl must be 1-{MaxUrlLength} characters.";
        }

        if (r.NotifyUrl is { Length: > MaxUrlLength })
        {
            return $"NotifyUrl must be at most {MaxUrlLength} characters.";
        }

        return null;
    }
}
