using Payment.Models.Requests;
using Payment.Models.Results;

namespace Payment.Models.Abstractions;

/// <summary>
/// Universal interface that every payment provider must implement.
/// Provider-specific failures (API errors, transport errors, timeouts) are reported
/// through the returned result objects; only caller-initiated cancellation throws.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Registers a new transaction and returns a redirect URL for the customer.
    /// </summary>
    Task<CreatePaymentResult> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current status of a previously created payment.
    /// </summary>
    Task<PaymentStatusResult> GetPaymentStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the signature / integrity of an inbound IPN notification.
    /// Returns true only when every field the provider signs is present and the signature matches.
    /// </summary>
    bool ValidateNotification(PaymentNotification notification);

    /// <summary>
    /// Confirms / settles a payment after a validated IPN notification.
    /// Providers that do not require an explicit confirmation step should
    /// return <see cref="ConfirmPaymentResult.Ok"/> immediately.
    /// </summary>
    Task<ConfirmPaymentResult> ConfirmPaymentAsync(
        ConfirmPaymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a full or partial refund for a completed payment.
    /// </summary>
    Task<RefundResult> RefundAsync(
        RefundRequest request,
        CancellationToken cancellationToken = default);
}
