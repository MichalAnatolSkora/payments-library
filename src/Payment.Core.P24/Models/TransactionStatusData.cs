using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

internal sealed class TransactionStatusData
{
    /// <summary>
    /// Transfer title
    /// </summary>
    [JsonPropertyName("statement")]
    public string? Statement { get; init; }

    /// <summary>
    /// Transaction ID
    /// </summary>
    [JsonPropertyName("orderId")]
    public long OrderId { get; init; }

    /// <summary>
    /// Transaction ID assigned by Merchant
    /// </summary>
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Transaction status
    /// 0 - no payment
    /// 1 - advance payment
    /// 2 - payment made
    /// 3 - payment returned
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; init; }

    /// <summary>
    /// Transaction amount
    /// </summary>
    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    /// <summary>
    /// Transaction currency
    /// </summary>
    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    /// <summary>
    /// Transaction registration date
    /// </summary>
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    /// <summary>
    /// Date of payment
    /// </summary>
    [JsonPropertyName("dateOfTransaction")]
    public string? DateOfTransaction { get; init; }

    /// <summary>
    /// Customer's e-mail
    /// </summary>
    [JsonPropertyName("clientEmail")]
    public string? ClientEmail { get; init; }

    /// <summary>
    /// Customer's hashed bank account number
    /// </summary>
    [JsonPropertyName("accountMD5")]
    public string? AccountMD5 { get; init; }

    /// <summary>
    /// Payment method ID
    /// </summary>
    [JsonPropertyName("paymentMethod")]
    public int? MethodId { get; init; }

    /// <summary>
    /// Transaction description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Customer's first name and surname
    /// </summary>
    [JsonPropertyName("clientName")]
    public string? ClientName { get; init; }

    /// <summary>
    /// Customer's address
    /// </summary>
    [JsonPropertyName("clientAddress")]
    public string? ClientAddress { get; init; }

    /// <summary>
    /// Customer's city
    /// </summary>
    [JsonPropertyName("clientCity")]
    public string? ClientCity { get; init; }

    /// <summary>
    /// Customer's zip code
    /// </summary>
    [JsonPropertyName("clientPostcode")]
    public string? ClientPostcode { get; init; }

    /// <summary>
    /// Batch number in which the transaction was paid
    /// </summary>
    [JsonPropertyName("batchId")]
    public int? BatchId { get; init; }

    /// <summary>
    /// Commission
    /// </summary>
    [JsonPropertyName("fee")]
    public string? Fee { get; init; }
}
