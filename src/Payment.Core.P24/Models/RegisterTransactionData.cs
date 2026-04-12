using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

internal sealed class RegisterTransactionData
{
    [JsonPropertyName("token")]
    public required string Token { get; init; }
}