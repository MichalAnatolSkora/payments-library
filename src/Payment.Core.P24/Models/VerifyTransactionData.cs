using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

/// <summary>
/// <c>data</c> payload of PUT /transaction/verify.
/// </summary>
internal sealed class VerifyTransactionData
{
    /// <summary>
    /// "success" when the transaction was verified.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}
