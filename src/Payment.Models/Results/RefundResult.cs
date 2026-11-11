namespace Payment.Models.Results;

public sealed class RefundResult
{
    public bool Success { get; init; }
    public string? RefundId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The provider refused the refund because it was already sent one with this <c>RequestId</c>, and this call
    /// refunded nothing. It says nothing about the earlier request: the provider may have accepted it (a retry after
    /// a lost answer) or refused it – Przelewy24 keeps the <c>RequestId</c> of a refund it refused too. Sending the
    /// same <c>RequestId</c> again only gets this answer again, so stop retrying and find out what became of the
    /// earlier request; a refund that did not go through needs a new <c>RequestId</c>.
    /// </summary>
    public bool DuplicateRequest { get; init; }

    public static RefundResult Ok(string? refundId = null) =>
        new() { Success = true, RefundId = refundId };

    public static RefundResult Fail(string error, int errorCode) =>
        new() { Success = false, ErrorMessage = error, ErrorCode = errorCode.ToString() };

    /// <summary>A failure with <see cref="DuplicateRequest"/> set.</summary>
    public static RefundResult Duplicate(string error, int errorCode) =>
        new() { Success = false, ErrorMessage = error, ErrorCode = errorCode.ToString(), DuplicateRequest = true };
}
