using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Payment.Core.P24.Security;

internal static class CryptographyProvider
{
    /// <summary>
    /// P24 computes signs over PHP <c>json_encode</c> output with
    /// <c>JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES</c>, so non-ASCII characters
    /// and slashes must be emitted verbatim rather than as \uXXXX escapes.
    /// </summary>
    private static readonly JsonSerializerOptions SignSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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
        }, SignSerializerOptions);
        return Sha384Hex(payload);
    }

    /// <summary>
    /// SHA-384 sign for /transaction/verify.
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
        }, SignSerializerOptions);
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
        }, SignSerializerOptions);
        return Sha384Hex(payload);
    }

    /// <summary>
    /// Constant-time comparison of two hex-encoded signs. Malformed or wrong-length input is a mismatch.
    /// </summary>
    internal static bool SignsMatch(string expectedHex, string? actualHex)
    {
        if (actualHex is null || actualHex.Length != expectedHex.Length)
        {
            return false;
        }

        byte[] actual;
        try
        {
            actual = Convert.FromHexString(actualHex);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expectedHex), actual);
    }

    private static string Sha384Hex(string input)
    {
        var bytes = SHA384.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
