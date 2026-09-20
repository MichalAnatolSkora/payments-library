using System.Diagnostics.CodeAnalysis;

namespace Payment.Models.Results;

/// <summary>
/// Outcome of a status lookup. <see cref="Status"/> is set only when <see cref="Success"/> is true;
/// otherwise <see cref="ErrorCode"/> and <see cref="ErrorMessage"/> describe why the lookup failed
/// (transaction not found, authentication error, transport failure, ...).
/// </summary>
public sealed class PaymentStatusResult
{
    [MemberNotNullWhen(true, nameof(Status))]
    public bool Success { get; init; }

    public PaymentStatus? Status { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static PaymentStatusResult Ok(PaymentStatus status) =>
        new() { Success = true, Status = status };

    public static PaymentStatusResult Fail(string error, int errorCode) =>
        new() { Success = false, ErrorMessage = error, ErrorCode = errorCode.ToString() };
}
