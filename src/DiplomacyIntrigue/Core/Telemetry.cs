using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Core
{
    /// <summary>
    /// Writes the numbers a balance pass needs, on its own, into the log.
    ///
    /// Why this exists: the acceptance test for the diplomacy pillar is a statement about a
    /// decade of campaign - average war length, whether alliances form and hold, whether
    /// anyone sits at permanent war. Measuring that by hand means someone watching a screen
    /// for hours and typing console commands, and the retail game has no console unless a
    /// separate mod provides one. So the mod reports on itself instead: leave a campaign
    /// running and the log is the dataset.
    ///
    /// Lines are prefixed and key=value so they can be grepped and parsed without a tool.
    /// </summary>
    public static class Telemetry
    {
        private const string SnapshotPrefix = "[SNAPSHOT]";
        private const string WarEndedPrefix = "[WAR-ENDED]";

        /// <summary>
        /// One line per week: the state of the world in numbers. Cheap enough to leave on.
        /// </summary>
        public static void WriteSnapshot(ModState state)
        {
            try
            {
                var ongoing = 0;
                var exhaustionTotal = 0f;
                var exhaustionSamples = 0;
                var longestWarDays = 0f;

                for (var i = 0; i < state.Wars.Count; i++)
                {
                    var war = state.Wars[i];
                    if (!war.IsOngoing) continue;

                    ongoing++;
                    exhaustionTotal += war.AggressorExhaustion + war.DefenderExhaustion;
                    exhaustionSamples += 2;
                    if (war.DaysElapsed > longestWarDays) longestWarDays = war.DaysElapsed;
                }

                var byType = new Dictionary<TreatyType, int>();
                for (var i = 0; i < state.Treaties.Count; i++)
                {
                    var treaty = state.Treaties[i];
                    if (!treaty.IsActive) continue;
                    byType.TryGetValue(treaty.Type, out var n);
                    byType[treaty.Type] = n + 1;
                }

                var kingdoms = 0;
                var atWar = 0;
                foreach (var kingdom in Kingdom.All)
                {
                    if (kingdom.IsEliminated) continue;
                    kingdoms++;

                    var fighting = false;
                    foreach (var _ in state.OngoingWarsOf(kingdom)) { fighting = true; break; }
                    if (fighting) atWar++;
                }

                var line = new StringBuilder(SnapshotPrefix);
                line.Append(" date=").Append(CampaignTime.Now.ToString());
                line.Append(" kingdoms=").Append(kingdoms);
                line.Append(" atWar=").Append(atWar);
                line.Append(" wars=").Append(ongoing);
                line.Append(" longestWarDays=").Append(longestWarDays.ToString("0"));
                line.Append(" avgExhaustion=").Append(
                    exhaustionSamples == 0 ? "0.0" : (exhaustionTotal / exhaustionSamples).ToString("0.0"));
                line.Append(" claims=").Append(CountLiveClaims(state));
                line.Append(" trustRecords=").Append(state.Trust.Count);
                line.Append(" fabrications=").Append(state.Fabrications.Count);

                foreach (TreatyType type in Enum.GetValues(typeof(TreatyType)))
                {
                    byType.TryGetValue(type, out var n);
                    line.Append(' ').Append(char.ToLowerInvariant(type.ToString()[0]))
                        .Append(type.ToString().Substring(1)).Append('=').Append(n);
                }

                Log.Info("Telemetry", line.ToString());
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "Snapshot failed.", ex);
            }
        }

        /// <summary>
        /// One line per war that ends, carrying the duration. This is the raw material for
        /// "wars average under three years" - the acceptance criterion for the pillar.
        /// </summary>
        public static void WriteWarEnded(WarRecord war)
        {
            try
            {
                Log.Info("Telemetry", WarEndedPrefix
                                      + " aggressor=" + Sanitise(war.Aggressor)
                                      + " defender=" + Sanitise(war.Defender)
                                      + " days=" + war.DaysElapsed.ToString("0")
                                      + " casusBelli=" + war.Justification
                                      + " finalScore=" + war.WarScore.ToString("0.0")
                                      + " exhaustion=" + war.AggressorExhaustion.ToString("0.0")
                                      + "/" + war.DefenderExhaustion.ToString("0.0")
                                      + " fiefsTaken=" + war.FiefsTakenByAggressor
                                      + "/" + war.FiefsTakenByDefender
                                      + " calledBy=" + Sanitise(war.CalledBy));
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "War-ended record failed.", ex);
            }
        }

        /// <summary>
        /// Writes a full, human-readable snapshot to its own file and returns the path.
        /// This is what the diplomacy menu offers, so the whole world state can be handed
        /// over without a console.
        /// </summary>
        public static string WriteReport(ModState state)
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord", "DiplomacyIntrigue", "Reports");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory,
                "report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");

            var sb = new StringBuilder();
            sb.AppendLine("Diplomacy & Intrigue " + SubModule.ModuleVersion + " report");
            sb.AppendLine("Campaign date: " + CampaignTime.Now);
            sb.AppendLine("Schema version: " + state.SchemaVersion);
            sb.AppendLine();

            sb.AppendLine("== Ongoing wars ==");
            var anyWar = false;
            for (var i = 0; i < state.Wars.Count; i++)
            {
                var war = state.Wars[i];
                if (!war.IsOngoing) continue;
                anyWar = true;
                sb.AppendLine("  " + war + " days=" + war.DaysElapsed.ToString("0"));
            }
            if (!anyWar) sb.AppendLine("  (none - the world is at peace)");
            sb.AppendLine();

            sb.AppendLine("== Concluded wars ==");
            var anyEnded = false;
            for (var i = 0; i < state.Wars.Count; i++)
            {
                var war = state.Wars[i];
                if (war.IsOngoing) continue;
                anyEnded = true;
                sb.AppendLine("  " + war + " lasted " + war.DaysElapsed.ToString("0") + " days");
            }
            if (!anyEnded) sb.AppendLine("  (none yet)");
            sb.AppendLine();

            sb.AppendLine("== Active treaties ==");
            var anyTreaty = false;
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (!treaty.IsActive) continue;
                anyTreaty = true;
                sb.Append("  ").Append(treaty).Append(" until ").Append(treaty.ExpiresOn);
                if (treaty.SubordinateParty != null)
                    sb.Append("  [").Append(treaty.SubordinateParty.Name)
                      .Append(" answers to ").Append(treaty.DominantParty?.Name).Append(']');
                if (treaty.TributeAmount > 0)
                    sb.Append("  [tribute ").Append(treaty.TributeAmount).Append(']');
                sb.AppendLine();
            }
            if (!anyTreaty) sb.AppendLine("  (none)");
            sb.AppendLine();

            sb.AppendLine("== Live claims ==");
            var anyClaim = false;
            for (var i = 0; i < state.Claims.Count; i++)
            {
                var claim = state.Claims[i];
                if (!claim.IsLive) continue;
                anyClaim = true;
                sb.AppendLine("  " + claim);
            }
            if (!anyClaim) sb.AppendLine("  (none)");
            sb.AppendLine();

            sb.AppendLine("== Trust ledger (directional) ==");
            if (state.Trust.Count == 0) sb.AppendLine("  (empty)");
            for (var i = 0; i < state.Trust.Count; i++) sb.AppendLine("  " + state.Trust[i]);
            sb.AppendLine();

            sb.AppendLine("== War weariness ==");
            if (state.Weariness.Count == 0) sb.AppendLine("  (none)");
            for (var i = 0; i < state.Weariness.Count; i++) sb.AppendLine("  " + state.Weariness[i]);

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Log.Info("Telemetry", "Report written to " + path);
            return path;
        }

        private static int CountLiveClaims(ModState state)
        {
            var n = 0;
            for (var i = 0; i < state.Claims.Count; i++) if (state.Claims[i].IsLive) n++;
            return n;
        }

        private static string Sanitise(Kingdom kingdom)
            => kingdom == null ? "none" : kingdom.Name.ToString().Replace(' ', '_');
    }
}
