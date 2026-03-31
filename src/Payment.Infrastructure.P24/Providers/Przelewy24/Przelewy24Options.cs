namespace Payment.Infrastructure.P24.Providers.Przelewy24;

public sealed class Przelewy24Options
{
    public required int MerchantId { get; init; }

    /// <summary>
    /// Point of Sale ID — usually equals MerchantId.
    /// </summary>
    public required int PosId { get; init; }

    /// <summary>
    /// Reports key ("Klucz do raportów") used as the Basic Auth password.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// CRC key used for SHA-384 signature computation.
    /// </summary>
    public required string CrcKey { get; init; }

    public bool Sandbox { get; init; } = false;
}
