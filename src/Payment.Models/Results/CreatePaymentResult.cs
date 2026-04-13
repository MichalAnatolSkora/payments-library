namespace Payment.Models.Results;

public sealed class CreatePaymentResult
{
    public bool Success { get; init; }

    public string? RedirectUrl { get; init; }

    public string? PaymentToken { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static CreatePaymentResult Ok(string redirectUrl, string? paymentToken = null) =>
        new() { Success = true, RedirectUrl = redirectUrl, PaymentToken = paymentToken };

    public static CreatePaymentResult Fail(string error, int errorCode) =>
        new() { Success = false, ErrorMessage = error, ErrorCode = errorCode.ToString() };
}
