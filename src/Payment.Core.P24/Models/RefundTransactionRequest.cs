using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

/// <summary>
/// Body of POST /transaction/refund. Authenticated with Basic auth only; there is no sign field.
/// </summary>
internal sealed class RefundTransactionRequest
{
    /// <summary>
    /// Unique request identifier (max 45 characters).
    /// </summary>
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    /// <summary>
    /// Unique identifier of the refund request in the merchant's system (max 35 characters).
    /// </summary>
    [JsonPropertyName("refundsUuid")]
    public required string RefundsUuid { get; init; }

    [JsonPropertyName("refunds")]
    public required IReadOnlyList<RefundItem> Refunds { get; init; }

    /// <summary>
    /// Optional URL to which refund status notifications are sent.
    /// </summary>
    [JsonPropertyName("urlStatus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UrlStatus { get; init; }
}
