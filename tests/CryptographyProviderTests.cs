using System.Security.Cryptography;
using System.Text;
using Payment.Core.P24.Security;

namespace Payment.Tests;

/// <summary>
/// Signs are SHA-384 over the exact JSON that PHP's json_encode produces with
/// JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES. Each test hashes that literal
/// independently so the serializer's escaping behaviour is actually exercised.
/// </summary>
public sealed class CryptographyProviderTests
{
    private static string Sha384(string s) =>
        Convert.ToHexString(SHA384.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    [Fact]
    public void RegisterSign_MatchesPhpJsonEncodeLayout()
    {
        var expected = Sha384("""{"sessionId":"order-1","merchantId":12345,"amount":1000,"currency":"PLN","crc":"crc-key"}""");

        Assert.Equal(expected, CryptographyProvider.ComputeRegisterSign(12345, "crc-key", "order-1", 1000, "PLN"));
    }

    [Fact]
    public void RegisterSign_DoesNotEscapeUnicodeOrSlashes()
    {
        var expected = Sha384("""{"sessionId":"zamówienie/1","merchantId":12345,"amount":1000,"currency":"PLN","crc":"crc-key"}""");

        Assert.Equal(expected, CryptographyProvider.ComputeRegisterSign(12345, "crc-key", "zamówienie/1", 1000, "PLN"));
    }

    [Fact]
    public void Signs_LeaveEmojiNoBreakSpaceAndDeleteUnescaped_AsPhpDoes()
    {
        // Built from code points so the source file stays plain: an emoji (outside the BMP), U+00A0 and U+007F.
        var text = $"a{char.ConvertFromUtf32(0x1F35C)}{(char)0xA0}{(char)0x7F}<>&'+z";
        var register = Sha384($$"""{"sessionId":"{{text}}","merchantId":12345,"amount":1000,"currency":"PLN","crc":"crc-key"}""");
        var webhook = Sha384(
            $$"""{"merchantId":12345,"posId":12345,"sessionId":"order-1","amount":1000,"originAmount":1000,"currency":"PLN","orderId":987654,"methodId":154,"statement":"{{text}}","crc":"crc-key"}""");

        Assert.Equal(register, CryptographyProvider.ComputeRegisterSign(12345, "crc-key", text, 1000, "PLN"));
        Assert.Equal(webhook, CryptographyProvider.ComputeWebhookSign(
            12345, "crc-key", 12345, "order-1", 1000, 1000, "PLN", 987654, 154, text));
    }

    [Fact]
    public void Signs_EscapeQuoteBackslashControlsAndLineSeparators_AsPhpDoes()
    {
        var sessionId = $"a\"b\\c\nd\te{(char)0x1F}f{(char)0x2028}g{(char)0x2029}h";
        var expected = Sha384(
            """{"sessionId":"a\"b\\c\nd\te\u001ff\u2028g\u2029h","orderId":987654,"amount":1000,"currency":"PLN","crc":"crc-key"}""");

        Assert.Equal(expected, CryptographyProvider.ComputeNotifySign("crc-key", sessionId, 987654, 1000, "PLN"));
    }

    [Fact]
    public void SignsMatch_AcceptsCaseInsensitiveHex()
    {
        var sign = CryptographyProvider.ComputeRegisterSign(1, "c", "s", 1, "PLN");

        Assert.True(CryptographyProvider.SignsMatch(sign, sign.ToUpperInvariant()));
    }

    [Fact]
    public void SignsMatch_RejectsNullWrongLengthAndNonHexOfSameLength()
    {
        var sign = CryptographyProvider.ComputeRegisterSign(1, "c", "s", 1, "PLN");
        var sameLengthNonHex = new string('z', sign.Length);
        var flippedLastNibble = sign[..^1] + (sign[^1] == '0' ? '1' : '0');

        Assert.False(CryptographyProvider.SignsMatch(sign, null));
        Assert.False(CryptographyProvider.SignsMatch(sign, sign[..^2]));
        Assert.False(CryptographyProvider.SignsMatch(sign, sameLengthNonHex));
        Assert.False(CryptographyProvider.SignsMatch(sign, flippedLastNibble));
    }

    [Fact]
    public void NotifySign_MatchesVerifyLayout()
    {
        var expected = Sha384("""{"sessionId":"order-1","orderId":987654,"amount":1000,"currency":"PLN","crc":"crc-key"}""");

        Assert.Equal(expected, CryptographyProvider.ComputeNotifySign("crc-key", "order-1", 987654, 1000, "PLN"));
    }

    [Fact]
    public void WebhookSign_MatchesIpnLayout()
    {
        var expected = Sha384(
            """{"merchantId":12345,"posId":12345,"sessionId":"order-1","amount":1000,"originAmount":1000,"currency":"PLN","orderId":987654,"methodId":154,"statement":"p24-abc","crc":"crc-key"}""");

        Assert.Equal(expected, CryptographyProvider.ComputeWebhookSign(
            12345, "crc-key", 12345, "order-1", 1000, 1000, "PLN", 987654, 154, "p24-abc"));
    }
}
