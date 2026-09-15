using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Core
{
    /// <summary>
    /// Developer console commands, all under the "diplomacy" namespace.
    /// Open the console with Alt+~ in a debug-enabled build, then e.g.
    ///     diplomacy.status
    ///     diplomacy.wars
    ///     diplomacy.claims
    ///
    /// These exist so every system can be inspected and driven before it has any UI.
    /// They are also how the systems get verified in a live campaign.
    /// </summary>
    public static class DebugCommands
    {
        private const string NoCampaign = "Diplomacy & Intrigue: no campaign loaded.";

        [CommandLineFunctionality.CommandLineArgumentFunction("status", "diplomacy")]
        public static string Status(List<string> args)
        {
            if (Campaign.Current == null) return NoCampaign;

            var state = CoreBehavior.State;
            if (state == null) return "Diplomacy & Intrigue: core behavior is not registered (mod failed to start).";

            var sb = new StringBuilder();
            sb.AppendLine("Diplomacy & Intrigue v" + SubModule.ModuleVersion + " - healthy: " + SubModule.Healthy);
            sb.AppendLine("Schema version: " + state.SchemaVersion);
            sb.AppendLine("Treaties: " + state.Treaties.Count);
            sb.AppendLine("War records: " + state.Wars.Count + " (" + CountOngoing(state) + " ongoing)");
            sb.AppendLine("Claims: " + state.Claims.Count);
            sb.AppendLine("Fief ledger: " + state.FiefHistory.Count + " records");
            sb.AppendLine("Weariness entries: " + state.Weariness.Count);
            sb.AppendLine("Fabrications running: " + state.Fabrications.Count);
            sb.AppendLine("Exhaustion rate: x" + Settings.Current.WarExhaustionRate.ToString("0.00"));
            sb.AppendLine("Log directory: " + (Log.LogDirectory ?? "unavailable"));
            return sb.ToString();
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("wars", "diplomacy")]
        public static string Wars(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var sb = new StringBuilder();
            var count = 0;
            for (var i = 0; i < state.Wars.Count; i++)
            {
                var war = state.Wars[i];
                if (!war.IsOngoing) continue;
                sb.AppendLine(war + " days=" + war.DaysElapsed.ToString("0")
                              + " casualties=" + war.AggressorCasualties + "/" + war.DefenderCasualties
                              + " fiefs=" + war.FiefsTakenByAggressor + "/" + war.FiefsTakenByDefender);
                count++;
            }
            return count == 0 ? "No ongoing wars on record." : sb.ToString();
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("treaties", "diplomacy")]
        public static string Treaties(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var sb = new StringBuilder();
            var count = 0;
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (!treaty.IsActive) continue;
                sb.AppendLine(treaty + " expires=" + treaty.ExpiresOn);
                count++;
            }
            return count == 0 ? "No active treaties on record." : sb.ToString();
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("claims", "diplomacy")]
        public static string Claims(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var sb = new StringBuilder();
            var count = 0;
            for (var i = 0; i < state.Claims.Count; i++)
            {
                var claim = state.Claims[i];
                if (!claim.IsLive) continue;
                sb.AppendLine(claim.ToString());
                count++;
            }
            return count == 0
                ? "No live claims. Claims appear when a fief is taken by siege or a village is raided."
                : sb.ToString();
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("weariness", "diplomacy")]
        public static string Weariness(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            if (state.Weariness.Count == 0)
                return "No kingdom carries war weariness. It accumulates when a war ends.";

            var sb = new StringBuilder();
            for (var i = 0; i < state.Weariness.Count; i++) sb.AppendLine(state.Weariness[i].ToString());
            return sb.ToString();
        }

        /// <summary>
        /// Prints the fief ledger for one settlement, which is the data behind ancestral
        /// claims. Usage: diplomacy.fief_history Pravend
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("fief_history", "diplomacy")]
        public static string FiefHistoryCommand(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            if (args == null || args.Count == 0)
                return "Usage: diplomacy.fief_history <settlement name>";

            var name = string.Join(" ", args);
            var settlement = FindSettlement(name);
            if (settlement == null) return "No settlement matching \"" + name + "\".";

            var sb = new StringBuilder();
            sb.AppendLine(settlement.Name + " - current holder: "
                          + (settlement.MapFaction == null ? "none" : settlement.MapFaction.Name.ToString()));
            var found = 0;
            for (var i = 0; i < state.FiefHistory.Count; i++)
            {
                var record = state.FiefHistory[i];
                if (record.Settlement != settlement) continue;
                sb.AppendLine("  " + record);
                found++;
            }
            if (found == 0) sb.AppendLine("  (no records yet)");
            return sb.ToString();
        }

        /// <summary>
        /// Starts fabricating a claim for the player kingdom. This is the mechanic that
        /// will sit behind the diplomacy UI in 1.8; until then the console is the only way
        /// in, for the player and for testing.
        /// Usage: diplomacy.fabricate_claim Pravend
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("fabricate_claim", "diplomacy")]
        public static string FabricateClaim(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            if (args == null || args.Count == 0)
                return "Usage: diplomacy.fabricate_claim <settlement name>";

            var kingdom = Clan.PlayerClan?.Kingdom;
            if (kingdom == null) return "You are not part of a kingdom.";

            var name = string.Join(" ", args);
            var settlement = FindSettlement(name);
            if (settlement == null) return "No settlement matching \"" + name + "\".";

            var attempt = ClaimRegistry.StartFabrication(state, kingdom, settlement, out var reason);
            return attempt == null
                ? "Cannot fabricate: " + reason
                : "Started: " + attempt + ". Cost: "
                  + DiplomacyConstants.FabricateClaimInfluenceCost + " influence, "
                  + DiplomacyConstants.FabricateClaimGoldCost + " denars. Exposure chance: "
                  + (DiplomacyConstants.FabricateClaimExposureChance * 100f).ToString("0") + "%.";
        }

        /// <summary>
        /// Runs the daily upkeep N times, driving the same functions the daily tick calls -
        /// not a simulation of them. Campaign time is untouched, so day counts and dates do
        /// not move; only the per-day accrual does.
        ///
        /// This exists because a campaign day takes real minutes to pass, which makes
        /// verifying accrual rates and running a balance pass impractical otherwise.
        /// Usage: diplomacy.tick_days 30
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("tick_days", "diplomacy")]
        public static string TickDays(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            if (args == null || args.Count == 0 || !int.TryParse(args[0], out var days))
                return "Usage: diplomacy.tick_days <number of days>";
            if (days < 1 || days > 400)
                return "Pick between 1 and 400 days.";

            for (var day = 0; day < days; day++)
            {
                WarExhaustion.DailyTick(state);
                ClaimRegistry.ExpireStale(state);
                ClaimRegistry.ResolveFabrications(state);
            }

            var sb = new StringBuilder();
            sb.AppendLine("Ran " + days + " day(s) of upkeep. Campaign time unchanged.");
            sb.AppendLine("Expected baseline exhaustion from elapsed time alone: "
                          + (days * DiplomacyConstants.ExhaustionPerDayAtWar
                             * Settings.Current.WarExhaustionRate).ToString("0.00"));
            for (var i = 0; i < state.Wars.Count; i++)
            {
                var war = state.Wars[i];
                if (war.IsOngoing) sb.AppendLine("  " + war);
            }
            return sb.ToString();
        }

        private static int CountOngoing(ModState state)
        {
            var n = 0;
            for (var i = 0; i < state.Wars.Count; i++) if (state.Wars[i].IsOngoing) n++;
            return n;
        }

        private static Settlement FindSettlement(string name)
        {
            foreach (var settlement in Settlement.All)
            {
                if (string.Equals(settlement.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase))
                    return settlement;
            }
            // Fall back to a prefix match so partial names work in the console.
            foreach (var settlement in Settlement.All)
            {
                var settlementName = settlement.Name?.ToString();
                if (settlementName != null &&
                    settlementName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    return settlement;
            }
            return null;
        }
    }
}
