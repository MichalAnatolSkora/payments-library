namespace PaymentsLibrary.Abstractions;

public sealed class ConfirmPaymentResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static ConfirmPaymentResult Ok() => new() { Success = true };

    public static ConfirmPaymentResult Fail(string errorCode, string errorMessage) =>
        new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
