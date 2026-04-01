namespace Payment.Models.Requests;

/// <summary>
/// Sent to the provider to confirm/settle a payment after a validated IPN notification.
/// Some providers (e.g. Przelewy24) require an explicit verify call; others treat
/// the IPN receipt as implicit confirmation.
/// </summary>
public sealed class ConfirmPaymentRequest
{
    public required string SessionId { get; init; }

    /// <summary>
    /// Provider-side order ID received in the IPN notification.
    /// </summary>
    public required string ProviderId { get; init; }

    public required int Amount { get; init; }
    public required string Currency { get; init; }
}
