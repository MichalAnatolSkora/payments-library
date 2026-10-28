namespace Payment.Models.Results;

public sealed class PaymentStatus
{
    public required string SessionId { get; init; }

    /// <summary>
    /// Provider-side order/transaction ID.
    /// </summary>
    public string? ProviderId { get; init; }

    public required int Amount { get; init; }

    public required string Currency { get; init; }

    public required PaymentState State { get; init; }

    /// <summary>
    /// Provider-specific method/channel identifier (optional).
    /// </summary>
    public string? MethodId { get; init; }

    /// <summary>
    /// When the payment was registered with the provider (optional).
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// When the customer paid (optional).
    /// </summary>
    public DateTimeOffset? PaidAt { get; init; }

    /// <summary>
    /// Provider commission in the lowest currency unit (optional).
    /// </summary>
    public int? Fee { get; init; }
}
