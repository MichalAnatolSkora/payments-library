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
    /// API key ("Klucz do raportów" in the P24 panel). Used for HTTP Basic auth together with <see cref="PosId"/>.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// CRC key from the P24 panel. Used to sign requests and verify notifications.
    /// </summary>
    public required string CrcKey { get; init; }

    /// <summary>
    /// Targets the P24 sandbox. Defaults to true so that an incomplete configuration
    /// never reaches production by accident; set to false explicitly to go live.
    /// </summary>
    public bool IsSandbox { get; init; } = true;

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
