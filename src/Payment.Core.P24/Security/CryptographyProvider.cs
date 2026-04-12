using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Payment.Core.P24.Security;

internal static class CryptographyProvider
{
    /// <summary>
    /// SHA-384 sign for /transaction/register.
    /// </summary>
    internal static string ComputeRegisterSign(
        int merchantId,
        string crc,
        string sessionId,
        int amount,
        string currency)
    {
        var payload = JsonSerializer.Serialize(new
        {
            sessionId,
            merchantId,
            amount,
            currency,
            crc,
        });
        return Sha384Hex(payload);
    }

    /// <summary>
    /// SHA-384 sign for IPN validation and /transaction/verify.
    /// </summary>
    internal static string ComputeNotifySign(
        string crc,
        string sessionId,
        long orderId,
        int amount,
        string currency)
    {
        var payload = JsonSerializer.Serialize(new
        {
            sessionId,
            orderId,
            amount,
            currency,
            crc,
        });
        return Sha384Hex(payload);
    }

    /// <summary>
    /// SHA-384 sign for /transaction/refund.
    /// </summary>
    internal static string ComputeRefundSign(
        int merchantId,
        string crc,
        string refundsUuid,
        int amount,
        string currency)
    {
        var payload = JsonSerializer.Serialize(new
        {
            refundsUuid,
            merchantId,
            amount,
            currency,
            crc,
        });
        return Sha384Hex(payload);
    }

    /// <summary>
    /// SHA-384 sign specifically for inbound webhook/IPN validation.
    /// </summary>
    internal static string ComputeWebhookSign(
        int merchantId,
        string crc,
        int posId,
        string sessionId,
        int amount,
        int originAmount,
        string currency,
        long orderId,
        int methodId,
        string statement)
    {
        var payload = JsonSerializer.Serialize(new
        {
            merchantId,
            posId,
            sessionId,
            amount,
            originAmount,
            currency,
            orderId,
            methodId,
            statement,
            crc,
        });
        return Sha384Hex(payload);
    }

    private static string Sha384Hex(string input)
    {
        var bytes = SHA384.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
