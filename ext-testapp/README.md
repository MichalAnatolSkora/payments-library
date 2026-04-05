# ExtTestApp

Angular + ASP.NET Core app for manually testing the payments library against P24 sandbox. Uses Clerk for authentication.

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- Clerk account (for auth)
- P24 sandbox credentials

## Setup

1. Create `appsettings.Local.json` (gitignored):

```json
{
  "Clerk": {
    "Authority": "https://<your-instance>.clerk.accounts.dev",
    "AuthorizedParty": "http://localhost:5201",
    "SecretKey": "sk_test_..."
  },
  "Przelewy24": {
    "MerchantId": 390264,
    "PosId": 390264,
    "ApiKey": "...",
    "CrcKey": "...",
    "Sandbox": true
  }
}
```

2. Create `src/environments/environment.ts` (gitignored):

```typescript
export const environment = {
  production: false,
  clerkPublishableKey: 'pk_test_...',
};
```

3. Build and run:

```bash
npx ng build
dotnet run
# open http://localhost:5201
```

## Dashboard tabs

### Config

Displays P24 credentials loaded from the server (`appsettings.Local.json`). Read-only.

### Create Payment

Registers a transaction via `IPaymentProvider.CreatePaymentAsync`. Returns a redirect URL — click it to open the P24 sandbox payment page. The `sessionId` is automatically propagated to other tabs.

### Payment Status

Checks the current state of a payment by session ID via `IPaymentProvider.GetPaymentStatusAsync`. Includes a legend mapping `PaymentState` enum values to P24 status codes.

### Confirm Payment

Confirms/settles a payment via `IPaymentProvider.ConfirmPaymentAsync`. Typically called after receiving a valid IPN notification.

### Refund

Issues a full or partial refund via `IPaymentProvider.RefundAsync`.

### Notifications

Lists IPN notifications received by the server's `/api/notify` endpoint, with validation results.

## IPN (Instant Payment Notification) flow

P24 sends an IPN to your `notifyUrl` when a payment status changes. The full flow:

1. **Create Payment** — set `notifyUrl` to a URL that P24 can reach (see below)
2. **User pays** — click the redirect URL and complete the payment in P24 sandbox
3. **P24 sends IPN** — POST to your `notifyUrl` with payment details and a SHA-384 signature
4. **Server validates** — `/api/notify` receives the payload, validates the signature via `IPaymentProvider.ValidateNotification`, and stores the result
5. **Check results** — open the **Notifications** tab to see received notifications and whether validation passed

### Exposing localhost to P24

P24 needs to reach your `notifyUrl` over the internet. Use a tunnel:

```bash
# ngrok
ngrok http 5201

# then set notifyUrl to e.g.:
# https://abc123.ngrok-free.app/api/notify
```

Without a tunnel, P24 cannot deliver notifications to `http://localhost:5201/api/notify`.

## API endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/api/config` | GET | Returns P24 config (from appsettings) |
| `/api/create-payment` | POST | Create payment via `IPaymentProvider` |
| `/api/create-payment-raw` | POST | Same, but returns raw HTTP request/response |
| `/api/payment-status/{sessionId}` | GET | Get payment status |
| `/api/payment-status-raw/{sessionId}` | GET | Same, raw HTTP |
| `/api/confirm-payment` | POST | Confirm payment |
| `/api/confirm-payment-raw` | POST | Same, raw HTTP |
| `/api/refund` | POST | Issue refund |
| `/api/refund-raw` | POST | Same, raw HTTP |
| `/api/notify` | POST | Receives P24 IPN webhooks |
| `/api/notifications` | GET | List received notifications |
| `/api/me` | GET | Current user (requires auth) |

Swagger UI available at `/swagger`.

## Raw HTTP capture

Endpoints ending with `-raw` bypass DI and create a `Przelewy24Provider` with a `CapturingHandler` that records the full HTTP request and response. Toggle "Show raw HTTP" in the UI to use these endpoints instead of the standard ones.
