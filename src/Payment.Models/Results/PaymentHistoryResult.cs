using System.Diagnostics.CodeAnalysis;

namespace Payment.Models.Results;

/// <summary>
/// One page of a payment history lookup. <see cref="Payments"/> is set only when <see cref="Success"/> is true;
/// otherwise <see cref="ErrorCode"/> and <see cref="ErrorMessage"/> describe why the lookup failed
/// (invalid date range, reporting not enabled for the account, transport failure, ...).
/// </summary>
public sealed class PaymentHistoryResult
{
    [MemberNotNullWhen(true, nameof(Payments))]
    public bool Success { get; init; }

    public IReadOnlyList<PaymentStatus>? Payments { get; init; }

    /// <summary>
    /// Pass as <c>PaymentHistoryRequest.PageToken</c> to fetch the next page; null on the last page.
    /// </summary>
    public string? NextPageToken { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static PaymentHistoryResult Ok(IReadOnlyList<PaymentStatus> payments, string? nextPageToken = null) =>
        new() { Success = true, Payments = payments, NextPageToken = nextPageToken };

    public static PaymentHistoryResult Fail(string error, int errorCode) =>
        new() { Success = false, ErrorMessage = error, ErrorCode = errorCode.ToString() };
}
