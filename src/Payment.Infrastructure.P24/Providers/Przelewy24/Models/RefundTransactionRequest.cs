using System.Text.Json.Serialization;

namespace Payment.Infrastructure.P24.Providers.Przelewy24.Models;

internal sealed class RefundTransactionRequest
{
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    [JsonPropertyName("refunds")]
    public required IReadOnlyList<RefundItem> Refunds { get; init; }
}

internal sealed class RefundItem
{
    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}
