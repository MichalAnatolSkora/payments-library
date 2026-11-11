using Payment.Core.P24.Formatting;

namespace Payment.Core.P24.Options;

public sealed class P24Options
{
    public static readonly Uri SandboxBaseAddress = new("https://sandbox.przelewy24.pl/");
    public static readonly Uri ProductionBaseAddress = new("https://secure.przelewy24.pl/");

    private Uri? _baseAddress;

    /// <summary>
    /// Merchant identification number.
    /// </summary>
    public required int MerchantId { get; init; }

    /// <summary>
    /// Shop identification number (defaults to merchant ID in the P24 panel).
    /// </summary>
    public required int PosId { get; init; }

    /// <summary>
    /// API key: "Klucz API" in the P24 panel's API configuration ("Klucz do raportów" in older versions of the panel).
    /// Used for HTTP Basic auth together with <see cref="PosId"/>. The panel's "Klucz do zamówień" is not used.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// CRC key: "Klucz do CRC" in the P24 panel's API configuration. Used to sign requests and verify notifications.
    /// </summary>
    public required string CrcKey { get; init; }

    /// <summary>
    /// Targets the P24 sandbox. Defaults to true so that an incomplete configuration
    /// never reaches production by accident; set to false explicitly to go live.
    /// </summary>
    public bool IsSandbox { get; init; } = true;

    /// <summary>
    /// Adjusts the texts P24 stores and shows – the transaction description, the customer's name (<c>client</c>) and
    /// the refund description – with <see cref="P24Text.Normalize"/>. P24 keeps them in ISO-8859-2, also when the
    /// request is sent as UTF-8, and turns every other character into "?": "Bánh Mì – #42" ends up as
    /// "Bánh M? ? #42" in P24's panel and on the customer's confirmation. The length limits are checked on the
    /// adjusted text, which is the text sent.
    /// Defaults to true; set it to false to send the texts exactly as given.
    /// </summary>
    public bool NormalizeText { get; init; } = true;

    /// <summary>
    /// P24 API origin. Derived from <see cref="IsSandbox"/> unless set explicitly,
    /// e.g. to point at a local mock server in tests.
    /// </summary>
    public Uri BaseAddress
    {
        get => _baseAddress ?? (IsSandbox ? SandboxBaseAddress : ProductionBaseAddress);
        init => _baseAddress = value;
    }
}
