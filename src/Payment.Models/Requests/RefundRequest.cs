namespace Payment.Models.Requests;

public sealed class RefundRequest
{
    /// <summary>Your original session/order ID used when creating the payment.</summary>
    public required string SessionId { get; init; }

    /// <summary>Provider-side order ID returned after payment completion.</summary>
    public string? ProviderId { get; init; }

    /// <summary>
    /// Amount to refund in minor currency units.
    /// Pass null for a full refund; the provider looks up the original amount.
    /// </summary>
    public int? Amount { get; init; }

    /// <summary>
    /// Refund currency. Ignored by providers that always refund in the
    /// original transaction currency (Przelewy24 does).
    /// </summary>
    public string? Currency { get; init; }

    /// <summary>
    /// Shown to the customer, e.g. in the bank transfer title. Przelewy24 caps it at 35 characters.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Idempotency key for this refund. Retrying with the same value will not issue a second refund.
    /// Generated when null. Przelewy24 caps it at 35 characters. Returned as <c>RefundResult.RefundId</c>.
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// URL to which the provider sends refund status notifications (optional).
    /// </summary>
    public string? NotifyUrl { get; init; }
}
