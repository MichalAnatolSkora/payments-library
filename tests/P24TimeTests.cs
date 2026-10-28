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
    [InlineData("2026-01-15 12:30:00", "2026-01-15T12:30:00+01:00")]
    [InlineData("2026-07-15 12:30:00", "2026-07-15T12:30:00+02:00")]
    [InlineData("2026-07-15T12:30:00", "2026-07-15T12:30:00+02:00")]
    [InlineData("2026-07-15", "2026-07-15T00:00:00+02:00")]
    public void Parse_AppliesWarsawOffsetOfThatDate(string value, string expectedValue)
    {
        var expected = DateTimeOffset.Parse(expectedValue, CultureInfo.InvariantCulture);

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

    // Fixed offsets rather than the system zone: the fallback matters exactly where the system has no tzdata.
    [Theory]
    [InlineData("2026-01-15T12:00:00", 1)]
    [InlineData("2026-03-29T01:59:00", 1)] // last minute of CET before the spring change
    [InlineData("2026-03-29T03:00:00", 2)] // first minute of CEST
    [InlineData("2026-07-15T12:00:00", 2)]
    [InlineData("2026-10-25T01:30:00", 2)] // CEST lasts until 03:00 on the day of the autumn change
    [InlineData("2026-10-25T02:30:00", 1)] // the repeated hour reads as standard time, as in the system zone
    [InlineData("2026-10-25T04:00:00", 1)] // after the autumn change
    [InlineData("2027-03-28T03:30:00", 2)]
    [InlineData("2027-10-30T12:00:00", 2)] // October 2027 has five Sundays; CEST lasts until the 31st
    public void FallbackZone_FollowsEuropeanSummerTime(string local, int offsetHours)
    {
        var dateTime = DateTime.Parse(local, CultureInfo.InvariantCulture);

        Assert.Equal(TimeSpan.FromHours(offsetHours), P24Time.CreateCentralEuropeanTime().GetUtcOffset(dateTime));
    }
}
