using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

/// <summary>
/// One entry of the <c>refunds</c> array sent to /transaction/refund.
/// Refunds are always issued in the original transaction currency, so no currency field exists.
/// </summary>
internal sealed class RefundItem
{
    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    /// <summary>
    /// Shown in the bank transfer title. P24 limits it to 35 characters.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}
