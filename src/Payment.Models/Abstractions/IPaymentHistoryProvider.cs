using Payment.Models.Requests;
using Payment.Models.Results;

namespace Payment.Models.Abstractions;

/// <summary>
/// Optional capability for providers with a reporting API: lists the payments the provider
/// recorded in a date range, like the transaction list in the provider's merchant panel.
/// Same error contract as <see cref="IPaymentProvider"/>: failures are reported through the
/// returned result; only caller-initiated cancellation throws.
/// </summary>
public interface IPaymentHistoryProvider
{
    /// <summary>
    /// Returns one page of payments. Request further pages with
    /// <see cref="PaymentHistoryResult.NextPageToken"/> until it is null.
    /// </summary>
    Task<PaymentHistoryResult> GetPaymentHistoryAsync(
        PaymentHistoryRequest request,
        CancellationToken cancellationToken = default);
}
