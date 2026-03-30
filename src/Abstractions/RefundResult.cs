namespace PaymentsLibrary.Abstractions;

public sealed class RefundResult
{
    public bool Success { get; init; }
    public string? RefundId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static RefundResult Ok(string? refundId = null) =>
        new() { Success = true, RefundId = refundId };

    public static RefundResult Fail(string errorCode, string errorMessage) =>
        new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
