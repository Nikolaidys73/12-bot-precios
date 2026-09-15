using System;
using TimeZoneConverter;

namespace L2PriceBot.App.Utils;

public static class TimezoneUtils
{
    public static DateTime GetLocalTime(DateTime utcTime, string timezoneId)
    {
        try
        {
            TimeZoneInfo tzi = TZConvert.GetTimeZoneInfo(timezoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tzi);
        }
        catch
        {
            return utcTime; // Fallback to UTC if timezone is invalid
        }
    }
}
