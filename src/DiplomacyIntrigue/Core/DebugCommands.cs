using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
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
            sb.AppendLine("Trust records: " + state.Trust.Count);
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
                sb.Append(treaty).Append(" expires=").Append(treaty.ExpiresOn);
                if (treaty.SubordinateParty != null)
                    sb.Append(" | ").Append(treaty.SubordinateParty.Name).Append(" answers to ")
                      .Append(treaty.DominantParty == null ? "?" : treaty.DominantParty.Name.ToString());
                if (treaty.TributeAmount > 0)
                    sb.Append(" | tribute ").Append(treaty.TributeAmount).Append(" from ")
                      .Append(treaty.TributePayer == null ? "?" : treaty.TributePayer.Name.ToString())
                      .Append(" due ").Append(treaty.NextTributeDue);
                sb.AppendLine();
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

        /// <summary>
        /// Trust is directional, so this prints both directions for every pair on record.
        /// Usage: diplomacy.trust   or   diplomacy.trust Vlandia
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("trust", "diplomacy")]
        public static string Trust(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            if (state.Trust.Count == 0)
                return "No trust records yet. Trust moves when treaties are honoured or broken, "
                       + "and when a war is declared without justification.";

            Kingdom filter = null;
            if (args != null && args.Count > 0)
            {
                var wanted = string.Join(" ", args);
                filter = FindKingdom(wanted);
                if (filter == null) return "No kingdom matching \"" + wanted + "\".";
            }

            var sb = new StringBuilder();
            for (var i = 0; i < state.Trust.Count; i++)
            {
                var record = state.Trust[i];
                if (filter != null && record.From != filter && record.To != filter) continue;
                sb.AppendLine(record.ToString());
            }
            var text = sb.ToString();
            return text.Length == 0 ? "No trust records involving that kingdom." : text;
        }

        /// <summary>
        /// Signs a treaty, going through the same CanSign checks the AI and the 1.8 UI use,
        /// so a refusal here is a real refusal rather than a console limitation.
        /// Usage: diplomacy.sign_treaty Vlandia | Battania | Alliance
        ///        diplomacy.sign_treaty Vlandia | Battania | TributaryPact | 500
        /// For the asymmetric types the second kingdom named is the subordinate.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("sign_treaty", "diplomacy")]
        public static string SignTreaty(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 3)
                return "Usage: diplomacy.sign_treaty <kingdom> | <kingdom> | <type> [| tribute]"
                       + Environment.NewLine + "Types: "
                       + string.Join(", ", Enum.GetNames(typeof(TreatyType)));

            var a = FindKingdom(parts[0]);
            var b = FindKingdom(parts[1]);
            if (a == null) return "No kingdom matching \"" + parts[0] + "\".";
            if (b == null) return "No kingdom matching \"" + parts[1] + "\".";

            TreatyType type;
            if (!Enum.TryParse(parts[2], true, out type))
                return "Unknown treaty type \"" + parts[2] + "\". Types: "
                       + string.Join(", ", Enum.GetNames(typeof(TreatyType)));

            var tribute = 0;
            if (parts.Count > 3) int.TryParse(parts[3], out tribute);

            var asymmetric = type == TreatyType.Vassalage || type == TreatyType.TributaryPact;
            var subordinate = (asymmetric || tribute > 0) ? b : null;

            var treaty = TreatyRegistry.Sign(state, a, b, type, out var reason, subordinate, tribute);
            return treaty == null ? "Refused: " + reason : "Signed: " + treaty;
        }

        /// <summary>
        /// Breaks a live treaty with its full reputational consequence. This is the
        /// deliberate-defiance path: never blocked, only expensive.
        /// Usage: diplomacy.break_treaty Vlandia | Battania | Alliance
        /// The first kingdom named is the one breaking it.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("break_treaty", "diplomacy")]
        public static string BreakTreaty(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 3)
                return "Usage: diplomacy.break_treaty <breaker> | <other party> | <type>";

            var breaker = FindKingdom(parts[0]);
            var other = FindKingdom(parts[1]);
            if (breaker == null) return "No kingdom matching \"" + parts[0] + "\".";
            if (other == null) return "No kingdom matching \"" + parts[1] + "\".";

            TreatyType type;
            if (!Enum.TryParse(parts[2], true, out type))
                return "Unknown treaty type \"" + parts[2] + "\".";

            var treaty = state.ActiveTreatyBetween(breaker, other, type);
            if (treaty == null) return "No active " + type + " between those kingdoms.";

            TreatyRegistry.Break(state, treaty, breaker);
            return breaker.Name + " broke the " + type + " with " + other.Name
                   + ". Victim trust in them is now "
                   + TrustRegistry.Get(state, other, breaker).ToString("0.0")
                   + ", and every other court took note.";
        }

        /// <summary>
        /// Reports whether a war would be allowed right now, and why not.
        /// Usage: diplomacy.can_war Vlandia | Battania
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("can_war", "diplomacy")]
        public static string CanWar(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.can_war <aggressor> | <target>";

            var a = FindKingdom(parts[0]);
            var b = FindKingdom(parts[1]);
            if (a == null || b == null) return "Kingdom not found.";

            var block = TreatyEnforcement.WhyWarBlocked(state, a, b);
            return block == TreatyEnforcement.Block.None
                ? a.Name + " may declare war on " + b.Name + "."
                : "Blocked: " + TreatyEnforcement.Explain(state, a, b, block) + ".";
        }

        /// <summary>
        /// Shows what a war has earned: the demand budget, the price of each term, and how
        /// close the other side is to signing. Same numbers the AI reads.
        /// Usage: diplomacy.peace_allowance Northern Empire | Khuzait
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("peace_allowance", "diplomacy")]
        public static string PeaceAllowance(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.peace_allowance <kingdom> | <kingdom>";

            var a = FindKingdom(parts[0]);
            var b = FindKingdom(parts[1]);
            if (a == null || b == null) return "Kingdom not found.";

            var war = state.OngoingWarBetween(a, b);
            if (war == null) return a.Name + " and " + b.Name + " are not at war.";

            var sb = new StringBuilder();
            sb.AppendLine("From " + a.Name + "'s side:");
            sb.AppendLine(PeaceTable.DescribeAllowance(state, war, a));
            sb.AppendLine();
            sb.AppendLine("From " + b.Name + "'s side:");
            sb.AppendLine(PeaceTable.DescribeAllowance(state, war, b));
            return sb.ToString();
        }

        /// <summary>
        /// Offers a peace package and applies it if the other side would sign. Goes through
        /// the same IsDemandable and WouldAccept checks the AI uses, so a refusal here is a
        /// real refusal.
        ///
        /// Usage: diplomacy.offer_peace &lt;winner&gt; | &lt;loser&gt; | &lt;terms&gt;
        ///   terms: white, prisoners, indemnity=5000, tribute=800, fief=Pravend
        ///   several terms are separated by commas
        /// Example: diplomacy.offer_peace Vlandia | Sturgia | fief=Varcheg, prisoners
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("offer_peace", "diplomacy")]
        public static string OfferPeace(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2)
                return "Usage: diplomacy.offer_peace <winner> | <loser> | <terms>" + Environment.NewLine
                       + "  terms: white, prisoners, indemnity=5000, tribute=800, fief=<name>";

            var winner = FindKingdom(parts[0]);
            var loser = FindKingdom(parts[1]);
            if (winner == null) return "No kingdom matching \"" + parts[0] + "\".";
            if (loser == null) return "No kingdom matching \"" + parts[1] + "\".";

            var war = state.OngoingWarBetween(winner, loser);
            if (war == null) return winner.Name + " and " + loser.Name + " are not at war.";

            var terms = new PeaceTerms(winner, loser);
            if (parts.Count > 2 && !ParseTerms(parts[2], terms, out var parseError))
                return parseError;

            if (!PeaceTable.IsDemandable(state, war, terms, out var notAllowed))
                return "Cannot demand that: " + notAllowed;

            if (!PeaceTable.WouldAccept(state, war, terms, out var refused))
                return "Refused: " + refused;

            return PeaceTable.Apply(state, war, terms, out var applyError)
                ? "Peace signed: " + terms
                : "Failed: " + applyError;
        }

        private static bool ParseTerms(string text, PeaceTerms terms, out string error)
        {
            error = null;
            foreach (var raw in text.Split(CommaSeparator))
            {
                var token = raw.Trim();
                if (token.Length == 0) continue;

                if (string.Equals(token, "white", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(token, "prisoners", StringComparison.OrdinalIgnoreCase))
                {
                    terms.ReleasePrisoners = true;
                    continue;
                }

                var split = token.Split(EqualsSeparator);
                if (split.Length != 2)
                {
                    error = "Do not understand \"" + token + "\".";
                    return false;
                }

                var key = split[0].Trim();
                var value = split[1].Trim();

                if (string.Equals(key, "indemnity", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(value, out var gold)) { error = "Bad indemnity \"" + value + "\"."; return false; }
                    terms.IndemnityGold = gold;
                }
                else if (string.Equals(key, "tribute", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(value, out var tribute)) { error = "Bad tribute \"" + value + "\"."; return false; }
                    terms.ImposeTributaryPact = true;
                    terms.TributePerPeriod = tribute;
                }
                else if (string.Equals(key, "fief", StringComparison.OrdinalIgnoreCase))
                {
                    var settlement = FindSettlement(value);
                    if (settlement == null) { error = "No settlement matching \"" + value + "\"."; return false; }
                    terms.FiefsCeded.Add(settlement);
                }
                else
                {
                    error = "Unknown term \"" + key + "\".";
                    return false;
                }
            }
            return true;
        }

        private static readonly char[] CommaSeparator = { ',' };
        private static readonly char[] EqualsSeparator = { '=' };

        /// <summary>Kingdom names contain spaces, so arguments are separated by a pipe.</summary>
        private static List<string> SplitOnPipe(List<string> args)
        {
            var joined = args == null ? "" : string.Join(" ", args);
            var parts = new List<string>();
            foreach (var piece in joined.Split(PipeSeparator))
            {
                var trimmed = piece.Trim();
                if (trimmed.Length > 0) parts.Add(trimmed);
            }
            return parts;
        }

        private static readonly char[] PipeSeparator = { '|' };

        private static Kingdom FindKingdom(string name)
        {
            foreach (var kingdom in Kingdom.All)
            {
                if (string.Equals(kingdom.Name == null ? null : kingdom.Name.ToString(), name,
                        StringComparison.OrdinalIgnoreCase))
                    return kingdom;
            }
            // Prefix match so partial names work in the console.
            foreach (var kingdom in Kingdom.All)
            {
                var kingdomName = kingdom.Name == null ? null : kingdom.Name.ToString();
                if (kingdomName != null && kingdomName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    return kingdom;
            }
            return null;
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
