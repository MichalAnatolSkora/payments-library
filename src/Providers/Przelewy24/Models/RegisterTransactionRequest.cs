using System.Text.Json.Serialization;

namespace Payment.Infrastructure.P24.Providers.Przelewy24.Models;

internal sealed class RegisterTransactionRequest
{
    [JsonPropertyName("merchantId")]
    public int MerchantId { get; init; }

    [JsonPropertyName("posId")]
    public int PosId { get; init; }

    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [JsonPropertyName("urlReturn")]
    public required string UrlReturn { get; init; }

    [JsonPropertyName("sign")]
    public required string Sign { get; init; }

    [JsonPropertyName("client")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Client { get; init; }

    [JsonPropertyName("urlStatus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UrlStatus { get; init; }

    [JsonPropertyName("country")]
    public string Country { get; init; } = "PL";

    [JsonPropertyName("language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; init; }

    [JsonPropertyName("encoding")]
    public string Encoding { get; init; } = "UTF-8";
}
