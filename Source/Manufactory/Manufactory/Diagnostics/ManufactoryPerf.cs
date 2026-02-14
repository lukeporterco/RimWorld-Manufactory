using System;
using System.Collections.Generic;
using System.Diagnostics;
using Verse;

namespace Manufactory.Diagnostics
{
    internal static class ManufactoryPerf
    {
        public static bool Enabled = false;

        private const int ReportEveryTicks = 6000;
        private static readonly Dictionary<string, Stat> stats = new Dictionary<string, Stat>();
        private static int nextReportTick = -1;

        private struct Stat
        {
            public long Calls;
            public long TotalTicks;
            public long MaxTicks;
        }

        public static long Begin()
        {
            return Enabled ? Stopwatch.GetTimestamp() : 0L;
        }

        public static void End(string key, long startStamp)
        {
            if (!Enabled || startStamp == 0L)
            {
                return;
            }

            long elapsed = Stopwatch.GetTimestamp() - startStamp;
            if (!stats.TryGetValue(key, out Stat stat))
            {
                stat = default;
            }

            stat.Calls++;
            stat.TotalTicks += elapsed;
            if (elapsed > stat.MaxTicks)
            {
                stat.MaxTicks = elapsed;
            }

            stats[key] = stat;
        }

        public static void ReportIfDue(int currentTick)
        {
            if (!Enabled)
            {
                return;
            }

            if (nextReportTick < 0)
            {
                nextReportTick = currentTick + ReportEveryTicks;
            }

            if (currentTick < nextReportTick)
            {
                return;
            }

            nextReportTick = currentTick + ReportEveryTicks;
            foreach (KeyValuePair<string, Stat> pair in stats)
            {
                double avgMs = (pair.Value.TotalTicks * 1000.0 / Stopwatch.Frequency) / Math.Max(1, pair.Value.Calls);
                double maxMs = pair.Value.MaxTicks * 1000.0 / Stopwatch.Frequency;
                Log.Message($"[ManufactoryPerf] {pair.Key} calls={pair.Value.Calls} avgMs={avgMs:F3} maxMs={maxMs:F3}");
            }

            stats.Clear();
        }
    }
}
