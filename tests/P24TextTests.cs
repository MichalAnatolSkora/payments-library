using System.Text;
using Payment.Core.P24.Formatting;

namespace Payment.Tests;

/// <summary>
/// Unit tests for <see cref="P24Text"/>: text adjusted to the ISO-8859-2 characters Przelewy24 can store.
/// </summary>
public sealed class P24TextTests
{
    private static readonly Encoding Iso88592 = CodePagesEncodingProvider.Instance.GetEncoding(
        28592, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)!;

    // ------------------------------------------------------------ kept as is

    [Theory]
    [InlineData("Order 42 (2 x 12.50 PLN) - thanks!")]
    [InlineData("Zażółć gęślą jaźń ZAŻÓŁĆ GĘŚLĄ JAŹŃ")]
    [InlineData("Příliš žluťoučký kůň úpěl ďábelské ódy")]
    [InlineData("Árvíztűrő tükörfúrógép")]
    [InlineData("Grüße aus Köln, Straße")]
    [InlineData("Đăng, Bánh, Tân")]
    public void Normalize_StorableText_Unchanged(string text)
    {
        Assert.Equal(text, P24Text.Normalize(text));
    }

    [Fact]
    public void Normalize_Empty_ReturnsEmpty()
    {
        Assert.Equal("", P24Text.Normalize(""));
    }

    [Fact]
    public void Normalize_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => P24Text.Normalize(null!));
    }

    // ----------------------------------------------------------- punctuation

    [Theory]
    [InlineData("Bánh Mì – #42", "Bánh Mi - #42")]
    [InlineData("a—b―c‐d", "a-b-c-d")]
    [InlineData("−5 °C", "-5 °C")]
    [InlineData("„Pod Lipą”", "\"Pod Lipą\"")]
    [InlineData("“quoted” «guillemets»", "\"quoted\" \"guillemets\"")]
    [InlineData("O’Brien’s ‘single’", "O'Brien's 'single'")]
    [InlineData("Wait…", "Wait...")]
    [InlineData("Pizza • Pasta · Salad", "Pizza - Pasta - Salad")]
    public void Normalize_TypographicPunctuation_BecomesPlain(string text, string expected)
    {
        Assert.Equal(expected, P24Text.Normalize(text));
    }

    // --------------------------------------------------------------- letters

    [Theory]
    [InlineData("ì", "i")]
    [InlineData("Nguyễn Văn Thọ", "Nguyen Văn Tho")]
    [InlineData("ố", "ô")]          // o + circumflex + acute: ô is ISO-8859-2, ố is not
    [InlineData("ǘ", "ü")]          // u + diaeresis + acute
    [InlineData("Đặng", "Đăng")]    // a + breve + dot below: ă is ISO-8859-2
    [InlineData("İstanbul", "Istanbul")]
    [InlineData("Ålesund, Ñandú", "Alesund, Nandú")]
    public void Normalize_AccentedLetters_KeepWhatIso88592Carries(string text, string expected)
    {
        Assert.Equal(expected, P24Text.Normalize(text));
    }

    [Theory]
    [InlineData("Ærøskøbing", "AEroskobing")]
    [InlineData("Œuvre, cœur", "OEuvre, coeur")]
    [InlineData("Þór, Ðóra, ðú", "THór, Dóra, dú")]
    [InlineData("ẞ", "SS")]
    [InlineData("Diyarbakır", "Diyarbakir")]
    [InlineData("ǣ", "ae")]         // æ + macron: decomposes to æ, which then has a fixed spelling
    public void Normalize_LettersWithoutDecomposition_GetFixedSpelling(string text, string expected)
    {
        Assert.Equal(expected, P24Text.Normalize(text));
    }

    [Theory]
    [InlineData("ﬁne ﬂour", "fine flour")]
    [InlineData("½ kg", "1/2 kg")]
    [InlineData("Brand™", "BrandTM")]
    [InlineData("①②", "12")]
    [InlineData("ＡＢＣ１２３", "ABC123")]
    [InlineData("x²", "x2")]
    [InlineData("10 €, 5 £", "10 EUR, 5 GBP")]
    [InlineData("© ® №7", "(c) (R) No7")]
    public void Normalize_CompatibilityFormsAndSigns_SpelledOut(string text, string expected)
    {
        Assert.Equal(expected, P24Text.Normalize(text));
    }

    [Fact]
    public void Normalize_DecomposedInput_IsComposedFirst()
    {
        Assert.Equal("é", P24Text.Normalize("é"));
        Assert.Equal("i", P24Text.Normalize("ì"));
    }

    // ----------------------------------------------------------- white space

    [Theory]
    [InlineData("  Order   42  ", "Order 42")]
    [InlineData("Line one\r\nLine two\tend", "Line one Line two end")]
    [InlineData("10 PLN net　x", "10 PLN net x")]
    public void Normalize_WhiteSpace_CollapsedAndTrimmed(string text, string expected)
    {
        Assert.Equal(expected, P24Text.Normalize(text));
    }

    [Theory]
    [InlineData("Ba­nh", "Banh")]      // soft hyphen, although ISO-8859-2 has it
    [InlineData("a​b‍c", "abc")]  // zero-width space and joiner
    [InlineData("‏abc‎", "abc")]  // direction marks
    [InlineData("﻿abc", "abc")]        // byte order mark
    public void Normalize_InvisibleFormatting_Removed(string text, string expected)
    {
        Assert.Equal(expected, P24Text.Normalize(text));
    }

    // ------------------------------------------------------ symbols, scripts

    [Theory]
    [InlineData("Anna 🍜", "Anna")]
    [InlineData("A 🍜 B", "A B")]
    [InlineData("👍🏽 ok", "ok")]                      // emoji with a skin tone modifier
    [InlineData("👨‍👩‍👧 family", "family")]              // joined with zero-width joiners
    [InlineData("🇵🇱 PL", "PL")]                      // regional indicators
    [InlineData("⭐️ 1️⃣", "1")]                      // variation selector, keycap
    [InlineData("🍜", "")]
    public void Normalize_EmojiAndPictographs_Removed(string text, string expected)
    {
        Assert.Equal(expected, P24Text.Normalize(text));
    }

    [Theory]
    [InlineData("Олена", "?????")]
    [InlineData("Йосип", "?????")]     // й decomposes, but и has no Latin form either
    [InlineData("東京", "??")]
    [InlineData("한국", "??")]          // a Korean syllable decomposes into three letters: still one "?"
    [InlineData("Anna Олена", "Anna ?????")]
    public void Normalize_OtherScripts_BecomeQuestionMarks(string text, string expected)
    {
        Assert.Equal(expected, P24Text.Normalize(text));
    }

    [Fact]
    public void Normalize_LoneSurrogate_BecomesQuestionMark()
    {
        Assert.Equal("a?b", P24Text.Normalize("a\uD800b"));
        Assert.Equal("a?", P24Text.Normalize("a\uDC00"));
    }

    [Theory]
    [InlineData("a\uFFFEb")]      // string.Normalize itself rejects this one
    [InlineData("a\uFFFFb")]
    [InlineData("a\uFDD0b")]
    [InlineData("a\U0001FFFEb")]
    public void Normalize_Noncharacter_BecomesQuestionMark(string text)
    {
        Assert.Equal("a?b", P24Text.Normalize(text));
    }

    // ------------------------------------------------------------ guarantees

    [Fact]
    public void Normalize_EveryBmpCharacter_GivesStorableIdempotentText()
    {
        for (var code = 0; code <= 0xFFFF; code++)
        {
            var text = $"a{(char)code}b";
            var normalized = P24Text.Normalize(text);

            AssertStorable(normalized, text);
            Assert.Equal(normalized, P24Text.Normalize(normalized));
        }
    }

    [Fact]
    public void Normalize_EverySupplementaryCharacter_GivesStorableIdempotentText()
    {
        for (var code = 0x10000; code <= 0x10FFFF; code++)
        {
            var text = $"a{char.ConvertFromUtf32(code)}b";
            var normalized = P24Text.Normalize(text);

            AssertStorable(normalized, text);
            Assert.Equal(normalized, P24Text.Normalize(normalized));
        }
    }

    private static void AssertStorable(string normalized, string input)
    {
        var bytes = Iso88592.GetBytes(normalized);   // throws on a character ISO-8859-2 lacks
        var code = char.IsSurrogatePair(input, 1) ? char.ConvertToUtf32(input, 1) : input[1];   // input = "a" + it + "b"
        Assert.True(Iso88592.GetString(bytes) == normalized, $"Not round-tripped for U+{code:X4}: '{normalized}'");
        Assert.DoesNotContain("  ", normalized);
        Assert.Equal(normalized.Trim(), normalized);
    }
}
