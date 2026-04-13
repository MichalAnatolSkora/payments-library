using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

/// <summary>
/// Transaction registration
/// </summary>
internal sealed class RegisterTransactionRequest
{
    /// <summary>
    /// Merchant identification number
    /// </summary>
    [JsonPropertyName("merchantId")]
    public required int MerchantId { get; init; }

    /// <summary>
    /// Shop identification number (defaults to merchant ID)
    /// </summary>
    [JsonPropertyName("posId")]
    public required int PosId { get; init; }

    /// <summary>
    /// Unique identifier from merchant's system
    /// </summary>
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Transaction amount expressed in the lowest currency unit, e.g. 1.23 PLN = 123
    /// </summary>
    [JsonPropertyName("amount")]
    public required int Amount { get; init; }

    /// <summary>
    /// Currency compatible with ISO, e.g. PLN
    /// </summary>
    [JsonPropertyName("currency")]
    public required string Currency { get; init; } = "PLN";

    /// <summary>
    /// Transaction description
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// Customer's e-mail
    /// </summary>
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    /// <summary>
    /// Customer's first name and surname
    /// </summary>
    [JsonPropertyName("client")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Client { get; init; }

    /// <summary>
    /// Country codes compatible with ISO, e.g. PL, DE, etc.
    /// </summary>
    [JsonPropertyName("country")]
    public required string Country { get; init; } = "PL";

    /// <summary>
    /// One of following language codes according to ISO 639-1:
    /// bg, cs, de, en, es, fr, hr, hu, it, nl, pl, pt, se, sk, ro
    /// </summary>
    [JsonPropertyName("language")]
    public required string Language { get; init; }

    /// <summary>
    /// URL address to which customer will be redirected when transaction is complete
    /// </summary>
    [JsonPropertyName("urlReturn")]
    public required string UrlReturn { get; init; }

    /// <summary>
    /// URL address to which transaction status will be sent
    /// </summary>
    [JsonPropertyName("urlStatus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UrlStatus { get; init; }

    /// <summary>
    /// Checksum of parameters:
    /// {"sessionId":"str","merchantId":int,"amount":int,"currency":"str","crc":"str"}
    /// </summary>
    [JsonPropertyName("sign")]
    public required string Sign { get; init; }

    /// <summary>
    /// Coding system for characters sent: ISO-8859-2, UTF-8, Windows-1250
    /// </summary>
    [JsonPropertyName("encoding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Encoding { get; init; } = "UTF-8";
}
