using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

/// <summary>
/// One entry of GET /api/v1/report/history: a batch, a transaction or a refund.
/// </summary>
internal sealed class ReportHistoryItem
{
    /// <summary>
    /// Entry type: batch, transaction or refund
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Amount expressed in the lowest currency unit, e.g. 1.23 PLN = 123
    /// </summary>
    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    /// <summary>
    /// Currency compatible with ISO, e.g. PLN
    /// </summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>
    /// Type-dependent details. The provider requests type=transaction, so only transaction details are bound.
    /// </summary>
    [JsonPropertyName("details")]
    public List<ReportTransactionDetails>? Details { get; init; }
}
