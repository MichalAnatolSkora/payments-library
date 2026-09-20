using Payment.Models.Abstractions;

namespace Payment.Models.Requests;

/// <summary>
/// Normalised representation of an inbound IPN / webhook notification.
/// Populate this from the raw HTTP request body before passing to
/// <see cref="IPaymentProvider.ValidateNotification"/> and
/// <see cref="IPaymentProvider.ConfirmPaymentAsync"/>.
/// Providers with a typed notification model usually offer a conversion helper
/// (for Przelewy24: <c>P24Notification.ToPaymentNotification()</c>).
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

    /// <summary>
    /// Every other field the provider includes in its signature, keyed by the provider's field name.
    /// A provider rejects the notification when a signed field is missing.
    /// </summary>
    public IReadOnlyDictionary<string, string> RawFields { get; init; } = new Dictionary<string, string>();
}
