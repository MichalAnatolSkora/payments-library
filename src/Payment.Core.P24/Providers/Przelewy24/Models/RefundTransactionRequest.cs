using System.Text.Json.Serialization;

namespace Payment.Core.P24.Providers.Przelewy24.Models;

internal sealed class RefundTransactionRequest
{
    [JsonPropertyName("merchantId")]
    public int MerchantId { get; init; }

    [JsonPropertyName("posId")]
    public int PosId { get; init; }

    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    [JsonPropertyName("refundsUuid")]
    public required string RefundsUuid { get; init; }

    [JsonPropertyName("refunds")]
    public required IReadOnlyList<RefundItem> Refunds { get; init; }

    [JsonPropertyName("sign")]
    public required string Sign { get; init; }
}

internal sealed class RefundItem
{
    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}
