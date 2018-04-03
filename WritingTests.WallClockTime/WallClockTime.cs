using Axinom.Toolkit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WritingTests.WallClockTime
{
    /// <summary>
    /// A leap-second-aware moment in real time, used all around the DASH scheduling logic.
    /// </summary>
    /// <remarks>
    /// We anchor at Unix epoch and measure true milliseconds (including those in leap seconds).
    /// Note that this is very different from "standard" Unix time which skips leap seconds and thus measures less time!
    /// 
    /// When translating to/from non-leapsecond-aware formats, we map the entire leap second to the moment that comes after it.
    /// So, for example, 1976-12-31 23:59:60 is represented as 1977-01-01 00:00:00 (which thereby lasts for two seconds).
    /// Fractional seconds within the leap second are not converted - time stands still for the duration of the leap second.
    /// 
    /// That the logic we use only supports positive leap seconds but it is not expected that negative ones will ever occur.
    /// </remarks>
    public struct WallClockTime : IComparable, IComparable<WallClockTime>
    {
        public const string DateTimeOffsetFormatWithMilliseconds = "yyyy'-'MM'-'dd HH':'mm':'ss.fff'Z'";

        /// <summary>
        /// The epoch from which we start to measure time.
        /// Values before this are still valid, just represented with negative values internally.
        /// </summary>
        public static readonly WallClockTime Epoch = new WallClockTime();

        /// <summary>
        /// True milliseconds since 1970-01-01 00:00:00Z, including those that occurred in leap seconds.
        /// This is NOT the same as DateTimeOffset.ToUnixTimeMilliseconds() which does not count time spent in leap seconds!
        /// </summary>
        public readonly long Milliseconds;

        // Private because we always want construction to specify what type of input we use.
        private WallClockTime(long trueMilliseconds)
        {
            Milliseconds = trueMilliseconds;
        }

        // DateTimeOffset uses pseudo-milliseconds, so just do a direct conversion.
        public static WallClockTime FromApproximateDateTimeOffset(DateTimeOffset value) =>
            new WallClockTime(PseudoMillisecondsToTrueMilliseconds(value.ToUnixTimeMilliseconds()));

        public static WallClockTime FromTrueMilliseconds(long trueMilliseconds) =>
            new WallClockTime(trueMilliseconds);

        public static WallClockTime FromPseudoMilliseconds(long pseudoMilliseconds) =>
            new WallClockTime(PseudoMillisecondsToTrueMilliseconds(pseudoMilliseconds));

        /// <summary>
        /// Get an approximate human-form timestamp for easy processing and display. Do not use in accurate logic.
        /// NB! DateTimeOffset cannot rerepsent moments inside leap seconds.
        /// </summary>
        public DateTimeOffset ToApproximateDateTimeOffset() => DateTimeOffset.FromUnixTimeMilliseconds(Milliseconds + GetAdjustmentForTrueMillisecondsToPseudoMilliseconds(Milliseconds));

        public override string ToString()
        {
            var dto = ToApproximateDateTimeOffset();
            var dtoWithoutMilliseconds = dto.Subtract(TimeSpan.FromMilliseconds(dto.Millisecond));

            var nextDto = AddTrueMilliseconds(1000).ToApproximateDateTimeOffset();
            var nextDtoWithoutMilliseconds = nextDto.Subtract(TimeSpan.FromMilliseconds(nextDto.Millisecond));

            // If the pseudo timeline is still in the same second after one second, we are in a leap second.
            var isInLeapSecond = dtoWithoutMilliseconds == nextDtoWithoutMilliseconds;

            if (isInLeapSecond)
            {
                // If we are in leap second then the DTO form already shows time past the leap second, so seek back first.
                dto = AddTrueMilliseconds(-1000).ToApproximateDateTimeOffset();

                // As we now seeked back from the leap second, we also get back the milliseconds as time no longer
                // stands still for the pseudo-timeline. All we need to do is replace the 59 with a 60 and we are done!
                return dto.ToString(DateTimeOffsetFormatWithMilliseconds, CultureInfo.InvariantCulture).Replace(":59:59.", ":59:60.");
            }
            else
            {
                return dto.ToString(DateTimeOffsetFormatWithMilliseconds, CultureInfo.InvariantCulture);
            }
        }

        public string ToDebugString()
        {
            return FormattableString.Invariant($"{ToString()} ({Milliseconds:N0} ms from epoch)");
        }

        public static bool operator <(WallClockTime a, WallClockTime b) => a.CompareTo(b) < 0;
        public static bool operator >(WallClockTime a, WallClockTime b) => a.CompareTo(b) > 0;
        public static bool operator <=(WallClockTime a, WallClockTime b) => a.CompareTo(b) <= 0;
        public static bool operator >=(WallClockTime a, WallClockTime b) => a.CompareTo(b) >= 0;
        public static bool operator ==(WallClockTime a, WallClockTime b) => a.CompareTo(b) == 0;
        public static bool operator !=(WallClockTime a, WallClockTime b) => a.CompareTo(b) != 0;
        public override bool Equals(object other) => CompareTo(other) == 0;

        public int CompareTo(object obj)
        {
            if (!(obj is WallClockTime))
                return 1;

            return CompareTo((WallClockTime)obj);
        }

        public int CompareTo(WallClockTime other)
        {
            return Milliseconds.CompareTo(other.Milliseconds);
        }

        public override int GetHashCode()
        {
            return Milliseconds.GetHashCode();
        }

        // Arithmetic operations all work on true time (including any leap seconds).
        public static TimeSpan operator -(WallClockTime a, WallClockTime b) =>
            TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond * (a.Milliseconds - b.Milliseconds));

        public static WallClockTime operator +(WallClockTime time, TimeSpan duration) =>
            new WallClockTime(time.Milliseconds + duration.Ticks / TimeSpan.TicksPerMillisecond);

        public static WallClockTime operator -(WallClockTime time, TimeSpan duration) =>
            new WallClockTime(time.Milliseconds - duration.Ticks / TimeSpan.TicksPerMillisecond);

        public WallClockTime AddTrueMilliseconds(long trueMilliseconds) =>
            new WallClockTime(Milliseconds + trueMilliseconds);

        public WallClockTime SubtractTrueMilliseconds(long trueMilliseconds) =>
            new WallClockTime(Milliseconds - trueMilliseconds);

        /// <summary>
        /// Gets the value to add to pseudo-milliseconds in order to transform them to true milliseconds.
        /// </summary>
        internal static long GetAdjustmentForPseudoMillisecondsToTrueMilliseconds(long pseudoMilliseconds)
        {
            // Exact match in pseudo-milliseconds means we consider that the leap second has already taken place.
            var candidates = _leapSecondAdjustments.Value.Where(a => a.StartTimestampPseudoMilliseconds <= pseudoMilliseconds);

            // If it is before any adjustments, the adjustment is zero.
            if (!candidates.Any())
                return 0;

            return candidates.Last().AdjustmentValue;
        }

        /// <summary>
        /// Gets the value to add to true milliseconds in order to transform them to pseudo-milliseconds.
        /// </summary>
        internal static long GetAdjustmentForTrueMillisecondsToPseudoMilliseconds(long trueMilliseconds)
        {
            var candidates = _leapSecondAdjustments.Value.Where(a => a.StartTimestampTrueMilliseconds <= trueMilliseconds);

            // If it is before any adjustments, the adjustment is zero.
            if (!candidates.Any())
                return 0;

            var adjustment = candidates.Last();

            // If a leap second is still ongoing, take off the remaining part of the leap second.
            var ongoingLeapSecondAdjustment = Math.Max(0, adjustment.StartTimestampTrueMilliseconds + 1000 - trueMilliseconds);

            return -adjustment.AdjustmentValue + ongoingLeapSecondAdjustment;
        }

        private static long PseudoMillisecondsToTrueMilliseconds(long pseudoMilliseconds) => pseudoMilliseconds + GetAdjustmentForPseudoMillisecondsToTrueMilliseconds(pseudoMilliseconds);

        private static long TrueMillisecondsToPseudoMilliseconds(long trueMilliseconds) => trueMilliseconds + GetAdjustmentForTrueMillisecondsToPseudoMilliseconds(trueMilliseconds);

        /// <summary>
        /// The logic assumes each adjustment adds one second to the offset.
        /// </summary>
        private struct LeapSecondAdjustment
        {
            /// <summary>
            /// Timestamp in pseudo-milliseconds (Unix timestamp) from which a new adjustment takes effect.
            /// Unix timeline does not experience leap seconds, so by this timestamp the leap second has elapsed.
            /// </summary>
            public long StartTimestampPseudoMilliseconds;

            /// <summary>
            /// Timestamp in true-milliseconds from which the new adjustment starts to gradually take effect.
            /// This is the moment on the true timeline when the leap second starts (and lasts for 1 second).
            /// The pseudo-millisecond clock is frozen during this true second.
            /// </summary>
            public long StartTimestampTrueMilliseconds;

            /// <summary>
            /// Sum of this leap second and all previous leap seconds, just for taking some algorithmic shortcuts.
            /// </summary>
            public long AdjustmentValue;
        }

        private static readonly DateTimeOffset _leapSecondDataEpoch = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// The database actually lists the offset between TAI and UTC.
        /// Besides leap seconds, there is a 10 second starting offset also embedded.
        /// It does not appear that we need this starting offset, though (we do not use TAI),
        /// so we need to get rid of it as all we want are the leap seconds.
        /// </summary>
        private const int TaiUtcOffsetMs = 10 * 1000;

        private static Lazy<List<LeapSecondAdjustment>> _leapSecondAdjustments = new Lazy<List<LeapSecondAdjustment>>(delegate
        {
            _log.Debug("Loading leap second database.");

            var result = new List<LeapSecondAdjustment>();

            using (var leapSecondList = new EmbeddedPackage(Assembly.GetExecutingAssembly(), "WritingTests.WallClockTime", "leap-seconds.list"))
            using (var reader = new StreamReader(Path.Combine(leapSecondList.Path, "leap-seconds.list")))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (line.StartsWith("#"))
                        continue;

                    var commentStartIndex = line.IndexOf('#');
                    if (commentStartIndex != -1)
                        line = line.Substring(0, commentStartIndex);

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Any remaining nonempty lines should be tab-delimited entries in our dictionary.
                    var components = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (components.Length != 2)
                    {
                        _log.Warning($"Unexpected line in leap seconds database: {line}");
                        continue;
                    }

                    // The file lists SI seconds since 1900, we work with UTC milliseconds since 1970.
                    // Example: 2303683200	12
                    // This is stamped 1973-01-01 00:00:00 UTC and is 94694400 seconds from Unix epoch
                    // The value 12 means that TAI and UTC are offset by 12. This is 2 leap seconds
                    // and the default 10 second offset that we want to discard. 2 is the number we want to use.

                    var timestampValue = long.Parse(components[0]); // Seconds from timestamp Epoch.
                    var timestampValueWithFixedEpoch = _leapSecondDataEpoch.AddSeconds(timestampValue).ToUnixTimeMilliseconds();

                    var adjustmentValue = long.Parse(components[1]) * 1000 - TaiUtcOffsetMs;

                    _log.Debug($"Leap second adjustment starting from {timestampValueWithFixedEpoch} ms on the Unix timeline is {adjustmentValue} ms.");

                    result.Add(new LeapSecondAdjustment
                    {
                        StartTimestampPseudoMilliseconds = timestampValueWithFixedEpoch,
                        StartTimestampTrueMilliseconds = timestampValueWithFixedEpoch + adjustmentValue - 1000,
                        AdjustmentValue = adjustmentValue
                    });
                }
            }

            return result;
        });

        private static readonly LogSource _log = Log.Default.CreateChildSource(nameof(WallClockTime));
    }
}
