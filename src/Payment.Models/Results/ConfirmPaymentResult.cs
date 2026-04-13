namespace Payment.Models.Results;

public sealed class ConfirmPaymentResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static ConfirmPaymentResult Ok() => new() { Success = true };

    public static ConfirmPaymentResult Fail(string error, int errorCode) =>
        new() { Success = false, ErrorMessage = error, ErrorCode = errorCode.ToString() };
}
