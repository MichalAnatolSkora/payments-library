# Payments Library for .NET

A lightweight .NET library for integrating with popular payment providers through a unified API.

## Supported Providers

- Stripe
- PayPal
- Adyen
- Braintree
- Przelewy24

## Installation

```bash
dotnet add package PaymentsLibrary
```

## Quick Start

```csharp
using PaymentsLibrary.Abstractions;
using PaymentsLibrary.Providers.Przelewy24;

IPaymentProvider provider = new Przelewy24Provider(new Przelewy24Options
{
    MerchantId = 12345,
    PosId      = 12345,
    ApiKey     = Environment.GetEnvironmentVariable("P24_API_KEY")!,
    CrcKey     = Environment.GetEnvironmentVariable("P24_CRC_KEY")!,
    Sandbox    = true,
});

// 1. Register a transaction and redirect the customer
var result = await provider.CreatePaymentAsync(new CreatePaymentRequest
{
    SessionId     = "order-42",
    Amount        = 9900,           // in groszy (99.00 PLN)
    Currency      = "PLN",
    Description   = "Order #42",
    CustomerEmail = "customer@example.com",
    ReturnUrl     = "https://yourshop.pl/payment/return",
    NotifyUrl     = "https://yourshop.pl/payment/notify",
});

if (result.Success)
    RedirectTo(result.RedirectUrl!);

// 2. On IPN webhook — validate then confirm
var notification = new PaymentNotification
{
    SessionId  = Request.Form["sessionId"]!,
    ProviderId = Request.Form["orderId"]!,
    Amount     = int.Parse(Request.Form["amount"]!),
    Currency   = Request.Form["currency"]!,
    Sign       = Request.Form["sign"]!,
};

if (provider.ValidateNotification(notification))
{
    await provider.ConfirmPaymentAsync(new ConfirmPaymentRequest
    {
        SessionId  = notification.SessionId,
        ProviderId = notification.ProviderId,
        Amount     = notification.Amount,
        Currency   = notification.Currency,
    });
}
```

## Features

- Single `IPaymentProvider` interface — swap providers without changing business logic
- Async-first API
- Strongly typed requests and responses
- Built-in SHA-384 signature signing and verification
- .NET 8+ support

## Configuration

Each provider is configured independently:

```csharp
// Przelewy24
IPaymentProvider provider = new Przelewy24Provider(new Przelewy24Options
{
    MerchantId = 12345,
    PosId      = 12345,
    ApiKey     = "...",   // "Klucz do raportów" from the P24 panel
    CrcKey     = "...",   // CRC key from the P24 panel
    Sandbox    = true,
});
```

## Operations

| Method | Description |
|---|---|
| `CreatePaymentAsync` | Register a transaction and get a customer redirect URL |
| `GetPaymentStatusAsync` | Retrieve current payment status by session ID |
| `ValidateNotification` | Verify IPN/webhook signature integrity |
| `ConfirmPaymentAsync` | Confirm/settle a payment after a validated notification |
| `RefundAsync` | Issue a full or partial refund |

## Contributing

Pull requests are welcome. Please open an issue first to discuss significant changes.

## License

MIT
