using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Core
{
    /// <summary>
    /// How long the mod's daily and weekly work takes, per handler and per campaign day (Phase 4,
    /// "daily/weekly tick budget measured"). The day's figure is the one a player feels: every
    /// daily handler runs inside the same engine tick, so their sum is the hitch at midnight.
    ///
    /// Measured by wrapping the handler where it is registered (<see cref="Wrap"/>), not inside each
    /// body, so a handler's own early returns and try/catch stay as they are and nothing is missed.
    /// Two Stopwatch reads per call; it is always on, because a budget that is only measured when
    /// someone remembers to switch it on is not measured on the save where the hitch happens.
    ///
    /// Nothing here is saved. The figures cover the session since load, or since the last reset.
    /// </summary>
    public static class TickBudget
    {
        private sealed class Entry
        {
            public long Calls;
            public long Ticks;
            public long MaxTicks;
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();

        private static int _day = int.MinValue;
        private static long _dayTicks;
        private static int _days;
        private static long _allDaysTicks;
        private static long _worstDayTicks;
        private static int _worstDay;

        /// <summary>A daily or weekly tick handler: counted per handler and in the day's total.</summary>
        public static Action Wrap(string name, Action handler) => () =>
        {
            var start = Stopwatch.GetTimestamp();
            try { handler(); }
            finally { Record(name, Stopwatch.GetTimestamp() - start, inDayTotal: true); }
        };

        /// <summary>
        /// An event handler with an argument (a battle ending): counted per handler but kept out of
        /// the day's total, which would otherwise mix work spread over the day into the midnight hitch.
        /// </summary>
        public static Action<T> Wrap<T>(string name, Action<T> handler) => arg =>
        {
            var start = Stopwatch.GetTimestamp();
            try { handler(arg); }
            finally { Record(name, Stopwatch.GetTimestamp() - start, inDayTotal: false); }
        };

        private static void Record(string name, long ticks, bool inDayTotal)
        {
            // Never let the measuring throw into the engine (CLAUDE.md §3): a failure here costs a
            // sample, not a campaign.
            try
            {
                if (!Entries.TryGetValue(name, out var e)) Entries[name] = e = new Entry();
                e.Calls++;
                e.Ticks += ticks;
                if (ticks > e.MaxTicks) e.MaxTicks = ticks;

                if (!inDayTotal) return;
                var day = (int)CampaignTime.Now.ToDays;
                if (day != _day) CloseDay(day);
                _dayTicks += ticks;
            }
            catch
            {
                // nothing useful to do
            }
        }

        private static void CloseDay(int newDay)
        {
            if (_day != int.MinValue)
            {
                _days++;
                _allDaysTicks += _dayTicks;
                if (_dayTicks > _worstDayTicks)
                {
                    _worstDayTicks = _dayTicks;
                    _worstDay = _day;
                }
            }
            _day = newDay;
            _dayTicks = 0;
        }

        public static void Reset()
        {
            Entries.Clear();
            _day = int.MinValue;
            _dayTicks = 0;
            _days = 0;
            _allDaysTicks = 0;
            _worstDayTicks = 0;
            _worstDay = 0;
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        /// <summary>One line for the weekly telemetry: the day totals, then each handler's mean and max.</summary>
        public static string DescribeLine()
        {
            var sb = new StringBuilder();
            sb.Append("days=").Append(_days)
              .Append(" dayMeanMs=").AppendMs(_days == 0 ? 0 : Ms(_allDaysTicks) / _days)
              .Append(" dayMaxMs=").AppendMs(Ms(_worstDayTicks));
            foreach (var kv in Entries)
            {
                var e = kv.Value;
                sb.Append(' ').Append(kv.Key).Append('=')
                  .AppendMs(e.Calls == 0 ? 0 : Ms(e.Ticks) / e.Calls).Append('/')
                  .AppendMs(Ms(e.MaxTicks));
            }
            return sb.ToString();
        }

        /// <summary>The table for <c>diplomacy.perf</c>.</summary>
        public static string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tick budget since load or the last reset (wall-clock ms, this machine):");
            sb.AppendLine("  whole days closed: " + _days
                          + ", mean " + Fmt(_days == 0 ? 0 : Ms(_allDaysTicks) / _days)
                          + ", worst " + Fmt(Ms(_worstDayTicks))
                          + (_days == 0 ? "" : " (day " + _worstDay + ")"));
            sb.AppendLine("  handler                        calls     mean      max     total");
            foreach (var kv in Entries)
            {
                var e = kv.Value;
                sb.AppendLine("  " + kv.Key.PadRight(28)
                              + e.Calls.ToString().PadLeft(8)
                              + Fmt(e.Calls == 0 ? 0 : Ms(e.Ticks) / e.Calls).PadLeft(9)
                              + Fmt(Ms(e.MaxTicks)).PadLeft(9)
                              + Fmt(Ms(e.Ticks)).PadLeft(10));
            }
            if (Entries.Count == 0) sb.AppendLine("  (nothing measured yet - wait for a daily tick)");
            return sb.ToString();
        }

        private static string Fmt(double ms) => ms.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        private static StringBuilder AppendMs(this StringBuilder sb, double value)
            => sb.Append(Math.Round(value, 2).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
