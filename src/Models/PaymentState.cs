namespace PaymentsLibrary.Models;

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