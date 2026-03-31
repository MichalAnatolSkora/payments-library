# Payments Library for .NET

A lightweight .NET library for integrating with payment providers through a unified API.

## Supported Providers

- Przelewy24

## Quick Start

```csharp
using Payment.Infrastructure.P24.Abstractions;
using Payment.Infrastructure.P24.Providers.Przelewy24;
using Payment.Models.Requests;

IPaymentProvider provider = new Przelewy24Provider(new Przelewy24Options
{
    MerchantId = 12345,
    PosId      = 12345,
    ApiKey     = "...",   // "Klucz do raportów" from the P24 panel
    CrcKey     = "...",   // CRC key from the P24 panel
    Sandbox    = true,
});

// 1. Register a transaction — redirect the customer to result.RedirectUrl
var result = await provider.CreatePaymentAsync(new CreatePaymentRequest
{
    SessionId     = "order-42",
    Amount        = 9900,           // in groszy (99.00 PLN)
    Currency      = "PLN",
    Description   = "Order #42",
    CustomerEmail = "customer@example.com",
    ReturnUrl     = "https://yourshop.pl/payment/return",
    NotifyUrl     = "https://yourshop.pl/payment/notify",
    Country       = "PL",
});

// 2. On IPN webhook — validate then confirm
var notification = new PaymentNotification
{
    SessionId  = Request.Form["sessionId"]!,
    ProviderId = Request.Form["orderId"]!,
    Amount     = int.Parse(Request.Form["amount"]!),
    Currency   = Request.Form["currency"]!,
    Sign       = Request.Form["sign"]!,
    RawFields  = Request.Form.ToDictionary(k => k.Key, v => v.Value.ToString()),
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

## DI Registration

```csharp
// Program.cs
builder.Services.AddPrzelewy24(builder.Configuration);
```

```json
// appsettings.json
{
  "Przelewy24": {
    "MerchantId": 0,
    "PosId": 0,
    "ApiKey": "",
    "CrcKey": "",
    "Sandbox": false
  }
}
```

## API

| Method | Description |
|---|---|
| `CreatePaymentAsync` | Register a transaction and get a customer redirect URL |
| `GetPaymentStatusAsync` | Retrieve current payment status by session ID |
| `ValidateNotification` | Verify IPN/webhook signature integrity |
| `ConfirmPaymentAsync` | Confirm/settle a payment after a validated notification |
| `RefundAsync` | Issue a full or partial refund |

## Test Console

The `testapp/` project is an ASP.NET Core app with a browser UI for manually testing all provider operations against the sandbox.

```bash
dotnet run --project testapp
# open http://localhost:5000
```

### Credential auto-fill

On startup the SPA calls `GET /api/config`, which reads the `Przelewy24` section from `appsettings.Development.json` and returns it to the browser. The credential fields in the sidebar are then pre-filled automatically — no need to type keys by hand every time.

To set your local credentials, edit `testapp/appsettings.Development.json` (already gitignored):

```json
{
  "Przelewy24": {
    "MerchantId": 390264,
    "PosId":      390264,
    "ApiKey":     "...",
    "CrcKey":     "...",
    "Sandbox":    true
  }
}
```

If the file is missing or the section is absent the UI simply starts with empty fields.

## License

MIT
