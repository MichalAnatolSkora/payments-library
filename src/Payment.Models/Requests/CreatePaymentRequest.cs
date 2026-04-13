namespace Payment.Models.Requests;

public sealed class CreatePaymentRequest
{
    /// <summary>
    /// Unique identifier from merchant's system
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Transaction amount expressed in the lowest currency unit, e.g. 1.23 PLN = 123
    /// </summary>
    public required int Amount { get; init; }

    /// <summary>
    /// Currency compatible with ISO, e.g. PLN
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Transaction description
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Customer's e-mail
    /// </summary>
    public required string CustomerEmail { get; init; }

    /// <summary>
    /// Customer's first name and surname
    /// </summary>
    public string? CustomerName { get; init; }

    /// <summary>
    /// Country codes compatible with ISO, e.g. PL, DE, etc.
    /// </summary>
    public required string Country { get; init; }

    /// <summary>
    /// One of following language codes according to ISO 639-1:
    /// bg, cs, de, en, es, fr, hr, hu, it, nl, pl, pt, se, sk, ro
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// URL address to which customer will be redirected when transaction is complete
    /// </summary>
    public required string ReturnUrl { get; init; }

    /// <summary>
    /// URL address to which transaction status will be sent
    /// </summary>
    public string? NotifyUrl { get; init; }
}
