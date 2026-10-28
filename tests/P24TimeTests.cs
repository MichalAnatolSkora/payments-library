using System.Globalization;
using Payment.Core.P24.Providers;

namespace Payment.Tests;

/// <summary>
/// P24 timestamps are Polish local time without an offset; they must come back with the
/// CET/CEST offset of their own date, whether or not the host has a time zone database.
/// </summary>
public sealed class P24TimeTests
{
    [Theory]
    [InlineData("2026-01-15 12:30:00", 1, 1)]
    [InlineData("2026-07-15 12:30:00", 7, 2)]
    [InlineData("2026-07-15T12:30:00", 7, 2)]
    public void Parse_AppliesWarsawOffsetOfThatDate(string value, int month, int offsetHours)
    {
        var expected = new DateTimeOffset(2026, month, 15, 12, 30, 0, TimeSpan.FromHours(offsetHours));

        var parsed = P24Time.Parse(value);

        Assert.Equal(expected, parsed);
        Assert.Equal(expected.Offset, parsed?.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0000-00-00 00:00:00")]
    [InlineData("15.07.2026")]
    public void Parse_MissingOrUnknownFormat_ReturnsNull(string? value)
    {
        Assert.Null(P24Time.Parse(value));
    }

    [Theory]
    [InlineData("2026-01-15T12:00:00")]
    [InlineData("2026-03-29T01:59:00")] // last minute of CET before the spring change
    [InlineData("2026-03-29T03:00:00")] // first minute of CEST
    [InlineData("2026-07-15T12:00:00")]
    [InlineData("2026-10-25T04:00:00")] // after the autumn change
    [InlineData("2027-03-28T03:30:00")]
    public void FallbackZone_MatchesSystemWarsawZone(string local)
    {
        var dateTime = DateTime.Parse(local, CultureInfo.InvariantCulture);
        var system = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

        Assert.Equal(system.GetUtcOffset(dateTime), P24Time.CreateCentralEuropeanTime().GetUtcOffset(dateTime));
    }
}
