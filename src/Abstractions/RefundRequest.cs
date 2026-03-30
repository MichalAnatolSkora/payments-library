namespace PaymentsLibrary.Abstractions;

public sealed class RefundRequest
{
    /// <summary>Your original session/order ID used when creating the payment.</summary>
    public required string SessionId { get; init; }

    /// <summary>Provider-side order ID returned after payment completion.</summary>
    public string? ProviderId { get; init; }

    /// <summary>
    /// Amount to refund in minor currency units.
    /// Pass null or the original amount for a full refund.
    /// </summary>
    public int? Amount { get; init; }

    public string? Description { get; init; }
}
