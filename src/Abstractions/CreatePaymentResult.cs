namespace PaymentsLibrary.Abstractions;

public sealed class CreatePaymentResult
{
    public bool Success { get; init; }

    /// <summary>URL to redirect the customer to in order to complete payment.</summary>
    public string? RedirectUrl { get; init; }

    /// <summary>Provider-specific token or payment ID (e.g. P24 transaction token).</summary>
    public string? PaymentToken { get; init; }

    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static CreatePaymentResult Ok(string redirectUrl, string? paymentToken = null) =>
        new() { Success = true, RedirectUrl = redirectUrl, PaymentToken = paymentToken };

    public static CreatePaymentResult Fail(string errorCode, string errorMessage) =>
        new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
