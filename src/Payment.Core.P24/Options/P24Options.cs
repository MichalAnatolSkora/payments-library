namespace Payment.Core.P24.Options;

public sealed class P24Options
{
    /// <summary>
    /// Gets a merchant identification number.
    /// </summary>
    public required int MerchantId { get; init; }

    /// <summary>
    /// Gets a shop identification number (defaults to merchant ID).
    /// </summary>
    public required int PosId { get; init; }

    /// <summary>
    /// Gets an API key.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets a cyclic redundancy check.
    /// </summary>
    public required string CrcKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether the environment is a sandbox.
    /// </summary>
    public bool IsSandbox { get; init; } = false;

    public Uri BaseAddress
    {
        get
        {
            var uri = IsSandbox
                ? "https://sandbox.przelewy24.pl"
                : "https://secure.przelewy24.pl";
            return new Uri(uri);
        }
    }
}
