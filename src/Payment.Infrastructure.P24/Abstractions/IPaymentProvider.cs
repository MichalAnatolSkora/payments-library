using Payment.Infrastructure.P24.Models;
using Payment.Models.Requests;
using Payment.Models.Results;

namespace Payment.Infrastructure.P24.Abstractions;

/// <summary>
/// Universal interface that every payment provider must implement.
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
    Task<PaymentStatus> GetPaymentStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the signature / integrity of an inbound IPN notification.
    /// Returns true when the notification is authentic.
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
