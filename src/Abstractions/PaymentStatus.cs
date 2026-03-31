namespace PaymentsLibrary.Abstractions;

public enum PaymentState
{
    /// <summary>Status could not be determined (e.g. session not found).</summary>
    Unknown,

    /// <summary>Payment registered but not yet initiated by the customer.</summary>
    Pending,

    /// <summary>Customer submitted payment; awaiting provider confirmation.</summary>
    Processing,

    /// <summary>Payment successfully confirmed by the provider.</summary>
    Completed,

    /// <summary>Payment was cancelled by the customer or provider.</summary>
    Cancelled,

    /// <summary>Payment session expired before completion.</summary>
    Expired,

    /// <summary>Full refund has been issued.</summary>
    Refunded,

    /// <summary>Partial refund has been issued; remainder was captured.</summary>
    PartialRefunded,

    /// <summary>Payment attempt failed (e.g. declined, error).</summary>
    Failed,
}

public sealed class PaymentStatus
{
    public required string SessionId { get; init; }

    /// <summary>Provider-side order/transaction ID.</summary>
    public string? ProviderId { get; init; }

    public required int Amount { get; init; }
    public required string Currency { get; init; }
    public required PaymentState State { get; init; }

    /// <summary>Provider-specific method/channel identifier (optional).</summary>
    public string? MethodId { get; init; }
}
