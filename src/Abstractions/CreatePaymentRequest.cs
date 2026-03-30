namespace PaymentsLibrary.Abstractions;

public sealed class CreatePaymentRequest
{
    /// <summary>Unique order/session ID from your system (max 100 chars).</summary>
    public required string SessionId { get; init; }

    /// <summary>Amount in minor currency units (e.g. groszy for PLN, cents for USD).</summary>
    public required int Amount { get; init; }

    /// <summary>ISO 4217 currency code, e.g. "PLN", "EUR", "USD".</summary>
    public required string Currency { get; init; }

    /// <summary>Human-readable description shown to the customer.</summary>
    public required string Description { get; init; }

    /// <summary>Customer e-mail address.</summary>
    public required string CustomerEmail { get; init; }

    /// <summary>URL the customer is redirected to after completing (or cancelling) payment.</summary>
    public required string ReturnUrl { get; init; }

    /// <summary>Webhook URL where the provider sends payment status notifications.</summary>
    public string? NotifyUrl { get; init; }

    /// <summary>Customer display name (optional but recommended).</summary>
    public string? CustomerName { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code (optional).</summary>
    public string? Country { get; init; }

    /// <summary>UI language code, e.g. "pl", "en" (optional).</summary>
    public string? Language { get; init; }
}
