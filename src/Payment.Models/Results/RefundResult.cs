namespace Payment.Models.Results;

public sealed class RefundResult
{
    public bool Success { get; init; }
    public string? RefundId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static RefundResult Ok(string? refundId = null) =>
        new() { Success = true, RefundId = refundId };

    public static RefundResult Fail(string error, int errorCode) =>
        new() { Success = false, ErrorMessage = error, ErrorCode = errorCode.ToString() };
}
