using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

/// <summary>
/// One entry of the <c>data</c> array returned by /transaction/refund (HTTP 201).
/// A refund can be rejected per item even though the HTTP status is 201.
/// </summary>
internal sealed class RefundResultItem
{
    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Was the refund attempt successful?
    /// </summary>
    [JsonPropertyName("status")]
    public bool Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
