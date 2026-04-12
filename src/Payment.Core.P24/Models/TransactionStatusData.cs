using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

internal sealed class TransactionStatusData
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("paymentMethod")]
    public int? MethodId { get; init; }
}
