using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Payment.Models.Requests;

namespace Payment.Core.P24.Models;

/// <summary>
/// Instant payment notification (IPN) payload sent by Przelewy24 to the merchant's <c>urlStatus</c>.
/// Reference: https://developers.przelewy24.pl/
/// </summary>
public sealed class P24Notification
{
    internal const string MerchantIdField = "merchantId";
    internal const string PosIdField = "posId";
    internal const string OriginAmountField = "originAmount";
    internal const string MethodIdField = "methodId";
    internal const string StatementField = "statement";

    /// <summary>
    /// Merchant identification number
    /// </summary>
    [JsonPropertyName("merchantId")]
    public required int MerchantId { get; init; }

    /// <summary>
    /// Shop identification number (defaults to merchant ID)
    /// </summary>
    [JsonPropertyName("posId")]
    public required int PosId { get; init; }

    /// <summary>
    /// Unique identifier from merchant's system
    /// </summary>
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Amount of paid transaction expressed in the lowest currency unit, e.g. 1.23 PLN = 123
    /// </summary>
    [JsonPropertyName("amount")]
    public required int Amount { get; init; }

    /// <summary>
    /// Amount the transaction was registered with (may differ from <see cref="Amount"/> for some methods).
    /// </summary>
    [JsonPropertyName("originAmount")]
    public required int OriginAmount { get; init; }

    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    /// <summary>
    /// Transaction ID assigned by Przelewy24.
    /// </summary>
    [JsonPropertyName("orderId")]
    public required long OrderId { get; init; }

    [JsonPropertyName("methodId")]
    public required int MethodId { get; init; }

    /// <summary>
    /// Transfer title. Part of the signature, so an absent value is signed as an empty string.
    /// </summary>
    [JsonPropertyName("statement")]
    public string? Statement { get; init; }

    [JsonPropertyName("sign")]
    public required string Sign { get; init; }

    /// <summary>
    /// Converts to the provider-agnostic notification model accepted by
    /// <c>IPaymentProvider.ValidateNotification</c>, carrying every signed field in <c>RawFields</c>.
    /// </summary>
    public PaymentNotification ToPaymentNotification() => new()
    {
        SessionId = SessionId,
        ProviderId = OrderId.ToString(),
        Amount = Amount,
        Currency = Currency,
        Sign = Sign,
        RawFields = new Dictionary<string, string>
        {
            [MerchantIdField] = MerchantId.ToString(),
            [PosIdField] = PosId.ToString(),
            [OriginAmountField] = OriginAmount.ToString(),
            [MethodIdField] = MethodId.ToString(),
            [StatementField] = Statement ?? string.Empty,
        },
    };

    /// <summary>
    /// Rebuilds the typed notification from the generic model. Fails when any signed field
    /// is missing or malformed; <c>statement</c> alone may be absent (treated as empty).
    /// </summary>
    internal static bool TryFromPaymentNotification(
        PaymentNotification notification,
        [NotNullWhen(true)] out P24Notification? result)
    {
        result = null;
        var fields = notification.RawFields;

        if (!long.TryParse(notification.ProviderId, out var orderId)
            || !TryGetInt(fields, MerchantIdField, out var merchantId)
            || !TryGetInt(fields, PosIdField, out var posId)
            || !TryGetInt(fields, OriginAmountField, out var originAmount)
            || !TryGetInt(fields, MethodIdField, out var methodId))
        {
            return false;
        }

        result = new P24Notification
        {
            MerchantId = merchantId,
            PosId = posId,
            SessionId = notification.SessionId,
            Amount = notification.Amount,
            OriginAmount = originAmount,
            Currency = notification.Currency,
            OrderId = orderId,
            MethodId = methodId,
            Statement = fields.TryGetValue(StatementField, out var statement) ? statement : string.Empty,
            Sign = notification.Sign,
        };
        return true;
    }

    private static bool TryGetInt(IReadOnlyDictionary<string, string> fields, string key, out int value)
    {
        value = 0;
        return fields.TryGetValue(key, out var raw) && int.TryParse(raw, out value);
    }
}
