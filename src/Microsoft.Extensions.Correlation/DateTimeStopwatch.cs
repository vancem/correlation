using System;
using System.Diagnostics;

namespace Microsoft.Extensions.Correlation
{
    public class DateTimeStopwatch
    {
        //last machine boot time if Stopwatch is HighResolution
        private static DateTime _stopwatchStartTime = Stopwatch.IsHighResolution ? 
            DateTime.UtcNow.AddSeconds(-1 * Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency) :
            DateTime.UtcNow;

        public static DateTime GetTime(long ticks)
        {
            return Stopwatch.IsHighResolution ? _stopwatchStartTime.AddSeconds(ticks / (double)Stopwatch.Frequency) : new DateTime(ticks);
        }

        public static DateTime GetTime()
        {
            return Stopwatch.IsHighResolution ? GetTime(Stopwatch.GetTimestamp()) : DateTime.UtcNow;
        }
    }
}
