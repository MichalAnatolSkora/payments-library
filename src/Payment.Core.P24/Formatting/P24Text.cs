using System.Buffers;
using System.Globalization;
using System.Text;

namespace Payment.Core.P24.Formatting;

/// <summary>
/// Adjusts text to the characters Przelewy24 can store.
/// </summary>
/// <remarks>
/// <para>
/// P24 keeps the transaction description, the customer's name and the refund description in ISO-8859-2 (Latin-2),
/// also when the request is sent as UTF-8, and turns every other character into "?": "Bánh Mì – #42" is stored and
/// shown as "Bánh M? ? #42". <see cref="Normalize"/> keeps every character ISO-8859-2 has and replaces the others
/// with their closest equivalent, so P24's panel and the customer's confirmation show readable text.
/// </para>
/// <para>Each character is handled by the first rule that applies:</para>
/// <list type="number">
/// <item><description>White space and control characters (tab, new line, no-break space) become a space. Runs of
/// spaces become one space, and the result is trimmed.</description></item>
/// <item><description>Invisible formatting characters (soft hyphen, zero-width space, direction marks) are
/// removed.</description></item>
/// <item><description>Characters ISO-8859-2 has stay as they are: ASCII and the letters of Polish, Czech, Slovak,
/// Hungarian, German, Romanian, Croatian, Slovene and other Central European languages, e.g. "Zażółć", "Příliš",
/// "Grüße", "Đ".</description></item>
/// <item><description>Typographic punctuation gets its plain form: dashes and the minus sign become "-", curly quotes
/// and guillemets "'" or "\"", bullets "-". Letters and signs without a Unicode decomposition get a fixed spelling:
/// "æ" → "ae", "œ" → "oe", "ø" → "o", "þ" → "th", "ı" → "i", "€" → "EUR", "©" → "(c)".</description></item>
/// <item><description>Other letters keep as many of their accents as ISO-8859-2 can carry: "ố" → "ô", "ǘ" → "ü",
/// "ễ" → "e", "ì" → "i". Compatibility forms are spelled out: "ﬁ" → "fi", "½" → "1/2", "™" → "TM", "…" → "...",
/// full-width "Ａ" → "A".</description></item>
/// <item><description>Emoji and other pictographic symbols are removed, with their modifiers and joiners.</description></item>
/// <item><description>Anything else, e.g. a letter of the Cyrillic or Chinese script, becomes "?", as P24 itself would
/// store it. To keep such text readable, transliterate it before passing it in: text P24 can already store passes
/// through unchanged.</description></item>
/// </list>
/// <para>The input is first composed (Unicode normalization form C), so "e" followed by a combining acute accent is
/// treated as "é". The result is always encodable in ISO-8859-2, and normalizing it again does not change it.</para>
/// </remarks>
public static class P24Text
{
    private const int Iso88592CodePage = 28592;

    /// <summary>Every character ISO-8859-2 can encode, taken from the codec rather than a hand-copied table.</summary>
    private static readonly SearchValues<char> Storable = SearchValues.Create(Iso88592Repertoire());

    /// <summary>Characters outside ISO-8859-2 that have no Unicode decomposition to fall back on.</summary>
    private static readonly Dictionary<int, string> Replacements = new()
    {
        // quotes and primes
        ['‘'] = "'", ['’'] = "'", ['‚'] = "'", ['‛'] = "'", ['‹'] = "'", ['›'] = "'",
        ['′'] = "'",
        ['“'] = "\"", ['”'] = "\"", ['„'] = "\"", ['‟'] = "\"", ['«'] = "\"", ['»'] = "\"",
        ['″'] = "\"",
        // minus sign, bullet, middle dot, fraction and division slashes
        ['−'] = "-", ['•'] = "-", ['·'] = "-", ['⁄'] = "/", ['∕'] = "/",
        // letters
        ['Æ'] = "AE", ['æ'] = "ae", ['Œ'] = "OE", ['œ'] = "oe", ['Ø'] = "O", ['ø'] = "o",
        ['Ð'] = "D", ['ð'] = "d", ['Þ'] = "TH", ['þ'] = "th", ['Ħ'] = "H", ['ħ'] = "h",
        ['ı'] = "i", ['Ŀ'] = "L", ['ŀ'] = "l", ['Ŋ'] = "N", ['ŋ'] = "n", ['ĸ'] = "k",
        ['Ŧ'] = "T", ['ŧ'] = "t", ['ẞ'] = "SS", ['Ə'] = "E", ['ə'] = "e", ['ƒ'] = "f",
        // signs
        ['€'] = "EUR", ['£'] = "GBP", ['©'] = "(c)", ['®'] = "(R)", ['№'] = "No",
        // what is left of ill-formed input (a lone surrogate) – a character was there and is lost
        ['�'] = "?",
    };

    /// <summary>
    /// Returns <paramref name="value"/> as Przelewy24 can store it: the characters ISO-8859-2 has are kept, the others
    /// are replaced by their closest equivalent, removed (emoji) or replaced by "?" (other scripts). See
    /// <see cref="P24Text"/> for the rules.
    /// </summary>
    /// <param name="value">The text to adjust.</param>
    /// <returns>The adjusted text; empty when nothing storable is left.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var output = new StringBuilder(value.Length);
        foreach (var rune in Compose(value).EnumerateRunes())
        {
            AppendRune(output, rune);
        }

        if (output.Length > 0 && output[^1] == ' ')
        {
            output.Length--;
        }

        return output.ToString();
    }

    private static void AppendRune(StringBuilder output, Rune rune)
    {
        if (Rune.IsControl(rune) || Rune.IsWhiteSpace(rune))
        {
            AppendSpace(output);
            return;
        }

        var category = Rune.GetUnicodeCategory(rune);
        if (category == UnicodeCategory.Format)
        {
            return;
        }

        if (rune.IsBmp && Storable.Contains((char)rune.Value))
        {
            output.Append((char)rune.Value);
            return;
        }

        if (Replacements.TryGetValue(rune.Value, out var replacement))
        {
            output.Append(replacement);
            return;
        }

        if (category == UnicodeCategory.DashPunctuation)
        {
            output.Append('-');
            return;
        }

        if (TryAppendDecomposed(output, rune))
        {
            return;
        }

        // A combining mark with no letter before it, an emoji, an emoji modifier: nothing readable to keep.
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark or UnicodeCategory.OtherSymbol or UnicodeCategory.ModifierSymbol)
        {
            return;
        }

        output.Append('?');
    }

    /// <summary>
    /// Spells out a character through its compatibility decomposition (NFKD): each base character keeps the combining
    /// marks that still give a single ISO-8859-2 character ("ố" = o + circumflex + acute → "ô"); the rest of the marks
    /// are dropped. A character none of whose parts can be stored (a Korean syllable is three) gives one "?", as it
    /// would in P24. False when the character has no decomposition.
    /// </summary>
    private static bool TryAppendDecomposed(StringBuilder output, Rune rune)
    {
        var original = rune.ToString();
        var decomposed = original.Normalize(NormalizationForm.FormKD);
        if (string.Equals(decomposed, original, StringComparison.Ordinal))
        {
            return false;
        }

        var spelled = new StringBuilder(decomposed.Length);
        string? cluster = null;
        foreach (var part in decomposed.EnumerateRunes())
        {
            if (IsMark(part))
            {
                if (cluster is not null
                    && (cluster + part).Normalize(NormalizationForm.FormC) is [var composed]
                    && Storable.Contains(composed))
                {
                    cluster = composed.ToString();
                }

                continue;
            }

            AppendCluster(spelled, cluster);
            cluster = part.ToString();
        }

        AppendCluster(spelled, cluster);

        if (spelled.Length > 1 && spelled.ToString().AsSpan().IndexOfAnyExcept('?') < 0)
        {
            spelled.Clear().Append('?');
        }

        // AppendSpace never puts a space first, so this cannot make a double space.
        output.Append(spelled);
        return true;
    }

    // The parts of a decomposition are decomposed already, so the rules apply to them without recursing further.
    private static void AppendCluster(StringBuilder output, string? cluster)
    {
        if (cluster is not null)
        {
            AppendRune(output, Rune.GetRuneAt(cluster, 0));
        }
    }

    private static void AppendSpace(StringBuilder output)
    {
        if (output.Length > 0 && output[^1] != ' ')
        {
            output.Append(' ');
        }
    }

    private static bool IsMark(Rune rune) => Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark
        or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    /// <summary>
    /// The input in normalization form C. <see cref="string.Normalize(NormalizationForm)"/> rejects ill-formed UTF-16
    /// and some noncharacters (U+FFFE), so a lone surrogate and every noncharacter – code points Unicode reserves for
    /// internal use, never for text – are replaced by U+FFFD first.
    /// </summary>
    private static string Compose(string value)
    {
        var interchangeable = new StringBuilder(value.Length);
        Span<char> buffer = stackalloc char[2];
        foreach (var rune in value.EnumerateRunes())   // a lone surrogate comes out as U+FFFD
        {
            var safe = IsNoncharacter(rune) ? Rune.ReplacementChar : rune;
            interchangeable.Append(buffer[..safe.EncodeToUtf16(buffer)]);
        }

        return interchangeable.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>U+FDD0–U+FDEF and the last two code points of every plane (U+FFFE, U+FFFF, U+1FFFE…).</summary>
    private static bool IsNoncharacter(Rune rune) => rune.Value is >= 0xFDD0 and <= 0xFDEF || (rune.Value & 0xFFFE) == 0xFFFE;

    private static string Iso88592Repertoire()
    {
        var encoding = CodePagesEncodingProvider.Instance.GetEncoding(Iso88592CodePage)
            ?? throw new InvalidOperationException("The ISO-8859-2 encoding is not available.");
        var bytes = new byte[256];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)i;
        }

        return encoding.GetString(bytes);
    }
}
