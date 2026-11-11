# Changelog

Changes to the `PaymentsLibrary` and `PaymentsLibrary.Models` packages. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [1.2.0] – 2026-11-11

### Added

- `RefundResult.DuplicateRequest` and the `RefundResult.Duplicate` factory in `PaymentsLibrary.Models`: the provider refused the refund because it was already sent one with this `RequestId`. `P24Provider.RefundAsync` sets it when P24 answers `Request already exists`; before, the answer could only be told from the error text. P24 keeps the `RequestId` of a refund it refused too, so sending the same one again never succeeds – the README has a section on retrying a refund.
- `RefundResult.RefundId` is also set on a failed refund once the request was sent (a timeout, a lost or refused answer), with the new `RefundResult.Fail(error, errorCode, refundId)` overload. Before, the identifier generated for a refund without a `RequestId` was lost on a timeout, so a retry got a new one and could refund twice.

### Fixed

- A refund P24 answers with 2xx and a body that cannot be read (not JSON, empty, an item in an unexpected shape) is no longer reported as `Refund failed.`: P24 may have accepted it, so the error message says the refund may have gone through and names the `RequestId` to retry with. The result is still a failure, with `RefundId` set.
- Signs are computed over JSON exactly as PHP `json_encode` writes it with `JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES`. Before, emoji and other characters outside the Basic Multilingual Plane, the no-break space, U+007F, U+0085 and U+FEFF were escaped as `\uXXXX`, so a `SessionId` with one of them was registered with a sign P24 would not accept, and a notification whose `statement` had one failed `ValidateNotification`. Signs of other texts are unchanged.
- `GetPaymentStatusAsync` no longer fails when P24 sends `fee`, `paymentMethod` or `batchId` in another shape (a number for the fee, an empty string): such a value is read as a number when it is one and as null otherwise, as in the payment history. A full refund, which looks the amount up, no longer fails with it.
- A response whose `Content-Type` names an unknown character set comes back as a failed result instead of an `InvalidOperationException`, and the `P24Provider` constructor throws `ArgumentNullException` for null options or a null `HttpClient`.
- A `SessionId` of `.` or `..` is refused before anything is sent. Before, such a payment could be registered, but `GetPaymentStatusAsync` then asked P24 for another path (`/api/v1/transaction/by/`), so its status – and with it a full refund – could not be read.

## [1.1.0] – 2026-11-05

### Added

- Payment history: `IPaymentHistoryProvider`, `PaymentHistoryRequest` and `PaymentHistoryResult` in `PaymentsLibrary.Models`, implemented by `P24Provider` with the P24 Report API (`GET /api/v1/report/history`) – the payments in a date range of up to 31 days, page by page. `PaymentStatus` gains `PaidAt` and `Fee`. `AddP24` registers `IPaymentHistoryProvider` and `P24Provider` next to `IPaymentProvider`, all on one named `HttpClient`.
- `P24Text.Normalize` (namespace `Payment.Core.P24.Formatting`): adjusts text to the characters Przelewy24 can store. P24 keeps descriptions and names in ISO-8859-2 and replaces every other character with `?`; the method keeps what ISO-8859-2 has, gives typographic punctuation its plain form, keeps as many accents as ISO-8859-2 can carry, removes emoji and replaces the rest with `?`. The result is always encodable in ISO-8859-2, and normalizing it again does not change it.
- `P24Options.NormalizeText` (configuration key `P24:NormalizeText`, default `true`).

### Changed

- `CreatePaymentAsync` and `RefundAsync` send the transaction description, the customer's name (`client`) and the refund description through `P24Text.Normalize`, unless `NormalizeText` is `false`. Before, P24 turned every character outside ISO-8859-2 into `?`: `Bánh Mì – #42` was stored as `Bánh M? ? #42`. White space in these texts is collapsed and trimmed, and a customer name or a refund description with nothing storable left is omitted.
- The length limits of these texts (1024, 40 and 35 characters) are checked on the adjusted text, which is the text sent. A text can get longer – `Æ` becomes `AE`, `…` becomes `...` – so one just under a limit may now fail it before any request is made.
- A changed text is logged at `Debug`, with the field and the session ID but without the text.
- Documentation: the API key is "Klucz API" in the current P24 panel ("Klucz do raportów" in older versions); "Klucz do zamówień" is not used. The README has a section on the panel's keys and IP addresses.

## [1.0.0]

First version.
