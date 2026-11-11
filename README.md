# Payments Library for .NET

A lightweight .NET library for integrating with payment providers through a unified API.

## Supported Providers

- Przelewy24

## Quick Start

```csharp
using Payment.Core.P24.Models;
using Payment.Core.P24.Options;
using Payment.Core.P24.Providers;
using Payment.Models.Abstractions;
using Payment.Models.Requests;

IPaymentProvider provider = new P24Provider(new P24Options
{
    MerchantId = 12345,
    PosId      = 12345,
    ApiKey     = "...",   // "Klucz API" from the P24 panel
    CrcKey     = "...",   // "Klucz do CRC" from the P24 panel
    IsSandbox  = true,
}, new HttpClient());

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
    Language      = "pl",
});

// 2. On IPN webhook — validate then confirm
//    P24 posts JSON; bind it to P24Notification (or build one from form fields).
var notification = await Request.ReadFromJsonAsync<P24Notification>();

if (notification is not null && provider.ValidateNotification(notification.ToPaymentNotification()))
{
    var confirm = await provider.ConfirmPaymentAsync(new ConfirmPaymentRequest
    {
        SessionId  = notification.SessionId,
        ProviderId = notification.OrderId.ToString(),
        Amount     = notification.Amount,
        Currency   = notification.Currency,
    });
    // confirm.Success / confirm.ErrorMessage
}

// 3. Status and refunds
var status = await provider.GetPaymentStatusAsync("order-42");
if (status.Success) Console.WriteLine(status.Status.State);

var refund = await provider.RefundAsync(new RefundRequest
{
    SessionId  = "order-42",
    ProviderId = "<orderId from IPN>",
    Amount     = null,            // null = full refund
    RequestId  = "refund-42-1",   // idempotency key, optional
});
```

## DI Registration

```csharp
// Program.cs
builder.Services.AddP24(builder.Configuration);
```

```json
// appsettings.json
{
  "P24": {
    "MerchantId": 0,
    "PosId": 0,
    "ApiKey": "",
    "CrcKey": "",
    "IsSandbox": true,
    "NormalizeText": true
  }
}
```

Notes:

- `IsSandbox` defaults to `true`; set it to `false` explicitly to go live.
- `BaseAddress` (optional) overrides the P24 origin, e.g. `"http://localhost:8089/"` for a mock server in tests.
- Options are validated at startup; a missing key fails fast with a message naming the field.
- If `ILogger` is registered, the provider logs failed calls and rejected notifications at `Warning`.
- `P24Provider.TestAccessAsync()` calls `GET /api/v1/testAccess` to check the credentials.
- Transport failures and timeouts come back as failed results, not exceptions; only caller cancellation throws.
- Besides `IPaymentProvider`, `AddP24` registers `IPaymentHistoryProvider` and `P24Provider` itself (for P24-specific calls such as `TestAccessAsync()`). All three share one named `HttpClient`, `nameof(IPaymentProvider)`; to add handlers (retries, logging) call `services.AddHttpClient<IPaymentProvider, P24Provider>().AddHttpMessageHandler(...)` after `AddP24` and they apply to all of them.
- `NormalizeText` (default `true`) adjusts the description and the names to the characters P24 can store – see [Text P24 stores](#text-p24-stores).

## API

| Method | Description |
|---|---|
| `CreatePaymentAsync` | Register a transaction and get a customer redirect URL |
| `GetPaymentStatusAsync` | Retrieve current payment status by session ID |
| `ValidateNotification` | Verify IPN/webhook signature integrity |
| `ConfirmPaymentAsync` | Confirm/settle a payment after a validated notification |
| `RefundAsync` | Issue a full or partial refund |
| `GetPaymentHistoryAsync` | List payments in a date range, page by page (`IPaymentHistoryProvider`) |

## Retrying a refund

`RefundRequest.RequestId` is the refund's idempotency key: a refund sent twice with the same value is refunded once. The second call fails with `RefundResult.DuplicateRequest` set (P24 answers `Request already exists`).

```csharp
var refund = await provider.RefundAsync(request);   // request.RequestId = "refund-42-1"
if (refund.DuplicateRequest)
{
    // P24 already has "refund-42-1". Do not send it again – the answer will not change.
}
```

- `DuplicateRequest` does not say the money went back. P24 keeps the `RequestId` of every refund request it received, also of one it refused, so the earlier request may have been accepted (a retry after a lost answer) or refused.
- For a refund of the whole payment, `GetPaymentStatusAsync` tells which: the state is `Refunded` once the refund went through. A partial refund cannot be told from the status; check it in the P24 panel.
- A refund that did not go through needs a new `RequestId`.
- Without a `RequestId` the provider generates one and returns it as `RefundResult.RefundId` – also on a failure once the request was sent, e.g. a timeout. P24 may have accepted that refund, so retry with `RequestId = refund.RefundId`; a retry without it gets a new identifier and can refund twice.
- When P24 answers 2xx but the answer cannot be read, the result is a failure whose `ErrorMessage` says the refund may have gone through. Treat it like a lost answer: check the refund, and retry only with the same `RequestId`.

## Payment History

`IPaymentHistoryProvider` is an optional capability, separate from `IPaymentProvider`: it lists the payments the provider recorded, like the transaction list in the merchant panel. `P24Provider` implements it with the P24 Report API (`GET /api/v1/report/history`).

```csharp
// P24Provider implements it; with DI, inject IPaymentHistoryProvider instead.
var history = (IPaymentHistoryProvider)provider;

string? pageToken = null;
do
{
    var page = await history.GetPaymentHistoryAsync(new PaymentHistoryRequest
    {
        From      = new DateOnly(2026, 10, 1),
        To        = new DateOnly(2026, 10, 31),
        PageToken = pageToken,  // null for the first page
    });
    if (!page.Success)
    {
        // page.ErrorCode / page.ErrorMessage
        break;
    }

    foreach (var payment in page.Payments)
    {
        // payment.SessionId, payment.ProviderId, payment.State, payment.Amount, payment.PaidAt, payment.Fee
    }

    pageToken = page.NextPageToken;
} while (pageToken is not null);
```

Notes:

- Przelewy24 enables the Report API per account on request (through your account manager or the P24 contact form). Until then the call fails with the error P24 returns.
- One request covers at most 31 days, both ends inclusive; longer ranges fail before anything is sent.
- `NextPageToken` continues the query it was issued for: pass it with the same `From` and `To`, otherwise the call fails before anything is sent.
- An entry P24 sends in an unexpected shape is skipped with a warning instead of failing the page.
- Each item is a `PaymentStatus`, the same model `GetPaymentStatusAsync` returns. P24 timestamps (Polish local time) come back as `DateTimeOffset`.
- Only transactions are listed for now; refunds and payout batches from the same report are not exposed yet.

## Text P24 stores

P24 keeps the transaction description, the customer's name and the refund description in ISO-8859-2 (Latin-2) – also when the request is UTF-8, as this library sends it – and replaces every other character with `?`. `Bánh Mì – #42` would show as `Bánh M? ? #42` in the P24 panel and on the customer's confirmation.

So the provider passes these three texts through `P24Text.Normalize` (namespace `Payment.Core.P24.Formatting`) before sending them:

| Given | Sent |
|---|---|
| `Zażółć gęślą jaźń`, `Příliš`, `Grüße` | unchanged – ISO-8859-2 has these letters |
| `Bánh Mì – #42` | `Bánh Mi - #42` |
| `„Pod Lipą”`, `O’Brien’s` | `"Pod Lipą"`, `O'Brien's` |
| `Nguyễn Văn Thọ` | `Nguyen Văn Tho` – each letter keeps the accents ISO-8859-2 can carry |
| `Ærøskøbing`, `10 €` | `AEroskobing`, `10 EUR` |
| `ﬁ`, `½`, `™`, `…` | `fi`, `1/2`, `TM`, `...` |
| `Anna 🍜` | `Anna` – emoji are removed |
| `Олена` | `?????` – as P24 itself would store it |

White space (new lines, tabs, no-break spaces) becomes single spaces, and the text is trimmed. The length limits – description 1024, customer name 40, refund description 35 characters – are checked on the text as sent. A customer name or a refund description with nothing storable left is omitted.

Letters of other scripts (Cyrillic, Chinese…) can't be spelled in ISO-8859-2 without knowing the language: transliterate them before passing them in – text P24 can already store passes through unchanged. `P24Text.Normalize` is public, so the same text can be shown to the customer before paying. To send the texts exactly as given, set `NormalizeText` to `false`.

## Przelewy24 panel

The panel's API configuration ("Konfiguracja API") and the options:

| Panel | Option |
|---|---|
| Klucz API ("Klucz do raportów" in older versions of the panel) | `ApiKey` |
| Klucz do CRC | `CrcKey` |
| Klucz do zamówień | not used |
| Adresy IP | see below |

`MerchantId` is the merchant's number and `PosId` the shop's, usually the same. The sandbox and production accounts have separate numbers and keys.

"Adresy IP": if you fill it in, list the public IPv4 address of every server that calls the API. Neither `secure.przelewy24.pl` nor `sandbox.przelewy24.pl` has an IPv6 address (checked November 2026), so P24 sees the IPv4 one. Notifications go the other way, from P24 to `NotifyUrl`.

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
