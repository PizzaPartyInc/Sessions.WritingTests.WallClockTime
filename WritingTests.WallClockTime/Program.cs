using Axinom.Toolkit;
using System;

namespace WritingTests.WallClockTime
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Default.RegisterListener(new ConsoleLogListener());

            var momentA = new DateTimeOffset(2016, 12, 31, 23, 59, 59, TimeSpan.Zero);
            var momentB = new DateTimeOffset(2017, 1, 1, 0, 0, 0, TimeSpan.Zero);

            var wctA = WallClockTime.FromApproximateDateTimeOffset(momentA);
            var wctB = WallClockTime.FromApproximateDateTimeOffset(momentB);

            var wctDiff = wctB - wctA;
            var diff = momentB - momentA;

            Log.Default.Info($"Between {momentA.ToDebugString()} and {momentB.ToDebugString()} there are {wctDiff.TotalMilliseconds} milliseconds in reality but .NET says there are {diff.TotalMilliseconds}.");

            Log.Default.Info($"1 second before {momentB.ToDebugString()} was really {wctB.SubtractTrueMilliseconds(1000)} but .NET thinks it was {momentB.AddMilliseconds(-1000).ToDebugString()}.");

            Log.Default.Info("What is time?");

            for (var after = 0; after < 3000; after += 200)
            {
                var realMoment = wctA.AddTrueMilliseconds(after);
                var moment = realMoment.ToApproximateDateTimeOffset();

                Log.Default.Info($"{realMoment} in real time is {moment.ToDebugString()} in .NET time");
            }
        }
    }
}
