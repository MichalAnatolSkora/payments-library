using System.Text.Json.Serialization;

namespace Payment.Models.Requests;

/// <summary>
/// Instant payment notification (IPN) payload sent by Przelewy24.
/// Reference: https://developers.przelewy24.pl/
/// </summary>
public sealed class InstantPaymentNotificationRequest
{
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

    [JsonPropertyName("originAmount")]
    public required int OriginAmount { get; init; }

    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    [JsonPropertyName("orderId")]
    public required long OrderId { get; init; }

    [JsonPropertyName("methodId")]
    public required int MethodId { get; init; }

    [JsonPropertyName("statement")]
    public string? Statement { get; init; }

    [JsonPropertyName("sign")]
    public required string Sign { get; init; }
}
