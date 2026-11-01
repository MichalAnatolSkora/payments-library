using System.Globalization;

namespace Payment.Core.P24.Providers;

/// <summary>
/// P24 sends timestamps as Polish local time without an offset, e.g. "2026-07-15 12:30:00".
/// </summary>
internal static class P24Time
{
    private static readonly string[] Formats = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd"];

    private static readonly TimeZoneInfo Warsaw = FindWarsaw();

    /// <summary>
    /// Returns null for a missing or unparseable value, e.g. the empty payment date of an unpaid transaction,
    /// and for a placeholder such as 0001-01-01 that has no UTC instant once the offset is applied.
    /// </summary>
    internal static DateTimeOffset? Parse(string? value)
    {
        if (!DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        var offset = Warsaw.GetUtcOffset(local);
        var utcTicks = local.Ticks - offset.Ticks;
        if (utcTicks < DateTime.MinValue.Ticks || utcTicks > DateTime.MaxValue.Ticks)
        {
            return null;
        }

        return new DateTimeOffset(local, offset);
    }

    private static TimeZoneInfo FindWarsaw()
        => TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Warsaw", out var zone)
            || TimeZoneInfo.TryFindSystemTimeZoneById("Central European Standard Time", out zone)
                ? zone
                : CreateCentralEuropeanTime();

    /// <summary>
    /// Fallback for hosts without a time zone database (e.g. containers without tzdata):
    /// CET with daylight saving from the last Sunday of March to the last Sunday of October.
    /// </summary>
    internal static TimeZoneInfo CreateCentralEuropeanTime()
    {
        var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday);
        var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1), start, end);

        return TimeZoneInfo.CreateCustomTimeZone(
            "Europe/Warsaw", TimeSpan.FromHours(1), "Central European Time",
            "Central European Standard Time", "Central European Summer Time", [rule]);
    }
}
