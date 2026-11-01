using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

/// <summary>
/// Transaction details of a <see cref="ReportHistoryItem"/>. Only the fields mapped to <c>PaymentStatus</c>
/// are bound; P24 also sends the customer's e-mail, name and address, which are deliberately not read.
/// Numbers are read leniently: a value in an unexpected shape becomes null instead of failing the page.
/// </summary>
internal sealed class ReportTransactionDetails
{
    /// <summary>
    /// Transaction ID
    /// </summary>
    [JsonPropertyName("orderId")]
    [JsonConverter(typeof(LenientIntegerConverter<long>))]
    public long? OrderId { get; init; }

    /// <summary>
    /// Transaction ID as spelled in the P24 report spec; accepted alongside <see cref="OrderId"/>.
    /// </summary>
    [JsonPropertyName("ordrId")]
    [JsonConverter(typeof(LenientIntegerConverter<long>))]
    public long? OrdrId { get; init; }

    /// <summary>
    /// Transaction ID assigned by Merchant
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    /// <summary>
    /// Transaction status
    /// 0 - unpaid
    /// 1 - paid
    /// 2 - paid and verified
    /// 3 - refunded
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(LenientIntegerConverter<int>))]
    public int? Status { get; init; }

    /// <summary>
    /// Transaction creation date
    /// </summary>
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    /// <summary>
    /// Date of payment, as in /transaction/by/sessionId (the report spec calls it the registration date)
    /// </summary>
    [JsonPropertyName("dateOfTransaction")]
    public string? DateOfTransaction { get; init; }

    /// <summary>
    /// Payment method ID
    /// </summary>
    [JsonPropertyName("paymentMethod")]
    [JsonConverter(typeof(LenientIntegerConverter<int>))]
    public int? MethodId { get; init; }

    /// <summary>
    /// Commission
    /// </summary>
    [JsonPropertyName("fee")]
    [JsonConverter(typeof(LenientIntegerConverter<int>))]
    public int? Fee { get; init; }
}
