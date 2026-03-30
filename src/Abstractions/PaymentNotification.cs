namespace PaymentsLibrary.Abstractions;

/// <summary>
/// Normalised representation of an inbound IPN / webhook notification.
/// Populate this from the raw HTTP request body before passing to
/// <see cref="IPaymentProvider.ValidateNotification"/> and
/// <see cref="IPaymentProvider.ConfirmPaymentAsync"/>.
/// </summary>
public sealed class PaymentNotification
{
    public required string SessionId { get; init; }

    /// <summary>Provider-side order ID (e.g. P24 orderId).</summary>
    public required string ProviderId { get; init; }

    public required int Amount { get; init; }
    public required string Currency { get; init; }

    /// <summary>Raw provider signature string for integrity verification.</summary>
    public required string Sign { get; init; }

    /// <summary>Full raw payload — kept for providers that need the original bytes for HMAC.</summary>
    public IReadOnlyDictionary<string, string> RawFields { get; init; } = new Dictionary<string, string>();
}
