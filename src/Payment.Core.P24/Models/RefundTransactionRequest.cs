using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

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
