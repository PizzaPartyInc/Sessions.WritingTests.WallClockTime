using System;
using System.Globalization;

namespace WritingTests.WallClockTime
{
    public static class ExtensionsForDateTimeOffset
    {
        public static string ToDebugString(this DateTimeOffset dto)
        {
            return dto.ToString(WallClockTime.DateTimeOffsetFormatWithMilliseconds, CultureInfo.InvariantCulture);
        }
    }
}
