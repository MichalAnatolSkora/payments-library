namespace PaymentsLibrary.Models;

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
}
