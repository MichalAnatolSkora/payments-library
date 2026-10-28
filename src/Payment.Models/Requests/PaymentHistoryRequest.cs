namespace Payment.Models.Requests;

public sealed class PaymentHistoryRequest
{
    /// <summary>
    /// First day of the range (inclusive).
    /// </summary>
    public required DateOnly From { get; init; }

    /// <summary>
    /// Last day of the range (inclusive). Przelewy24 allows at most 31 days per request.
    /// </summary>
    public required DateOnly To { get; init; }

    /// <summary>
    /// <c>PaymentHistoryResult.NextPageToken</c> from the previous page; null for the first page.
    /// The token continues the original query, so keep <see cref="From"/> and <see cref="To"/> unchanged.
    /// </summary>
    public string? PageToken { get; init; }
}
