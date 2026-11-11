using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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
        return Sha384Hex(string.Create(CultureInfo.InvariantCulture,
            $$"""{"sessionId":{{Quote(sessionId)}},"merchantId":{{merchantId}},"amount":{{amount}},"currency":{{Quote(currency)}},"crc":{{Quote(crc)}}}"""));
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
        return Sha384Hex(string.Create(CultureInfo.InvariantCulture,
            $$"""{"sessionId":{{Quote(sessionId)}},"orderId":{{orderId}},"amount":{{amount}},"currency":{{Quote(currency)}},"crc":{{Quote(crc)}}}"""));
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
        return Sha384Hex(string.Create(CultureInfo.InvariantCulture,
            $$"""{"merchantId":{{merchantId}},"posId":{{posId}},"sessionId":{{Quote(sessionId)}},"amount":{{amount}},"originAmount":{{originAmount}},"currency":{{Quote(currency)}},"orderId":{{orderId}},"methodId":{{methodId}},"statement":{{Quote(statement)}},"crc":{{Quote(crc)}}}"""));
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

    /// <summary>
    /// A JSON string exactly as PHP <c>json_encode</c> writes it with
    /// <c>JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES</c>, which is what P24 computes signs over: only the quote,
    /// the backslash, the control characters below U+0020 and the line separators U+2028 and U+2029 are escaped
    /// (the last ones as lower-case \uXXXX). Everything else is written as it is, slashes, non-ASCII letters, the
    /// no-break space and emoji included. <see cref="System.Text.Json"/> has no encoder that leaves all of these
    /// alone, so the payloads are written by hand.
    /// </summary>
    private static string Quote(string? value)
    {
        if (value is null)
        {
            return "null";
        }

        var json = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    json.Append("\\\"");
                    break;
                case '\\':
                    json.Append("\\\\");
                    break;
                case '\b':
                    json.Append("\\b");
                    break;
                case '\f':
                    json.Append("\\f");
                    break;
                case '\n':
                    json.Append("\\n");
                    break;
                case '\r':
                    json.Append("\\r");
                    break;
                case '\t':
                    json.Append("\\t");
                    break;
                case < ' ' or (char)0x2028 or (char)0x2029:
                    json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    json.Append(c);
                    break;
            }
        }

        return json.Append('"').ToString();
    }

    private static string Sha384Hex(string input)
    {
        var bytes = SHA384.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
