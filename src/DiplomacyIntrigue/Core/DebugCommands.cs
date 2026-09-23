using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
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

            for (var day = 0; day < days; day++) RunDailyUpkeep(state);

            var sb = new StringBuilder();
            sb.AppendLine("Ran " + days + " day(s) of upkeep. Campaign time unchanged.");
            sb.AppendLine("Anything measured in dates - treaty expiry, tribute due, the revolt"
                          + " countdown - cannot move while the clock is frozen.");
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
        /// Asks every clan of a kingdom how it would vote on a real policy decision, and shows
        /// where a bloc overrode a clan's own preference.
        ///
        /// Builds a genuine <c>KingdomPolicyDecision</c> and drives the same
        /// <c>DetermineSupportOption</c> the engine drives, so the bloc patch is exercised
        /// rather than simulated - but **nothing is applied**: the decision is never submitted
        /// and no vote is cast. There is no console command in v1.4.8 that opens a kingdom
        /// decision for real, and the AI only raises them on its own schedule, so without this
        /// the vote path could not be observed at all.
        ///
        /// Usage: diplomacy.test_vote            (the player's kingdom)
        ///        diplomacy.test_vote Khuzait
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_vote", "diplomacy")]
        public static string TestVote(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var kingdom = Clan.PlayerClan?.Kingdom;
            if (args != null && args.Count > 0)
            {
                var wanted = string.Join(" ", args);
                kingdom = FindKingdom(wanted);
                if (kingdom == null) return "No kingdom matching \"" + wanted + "\".";
            }
            if (kingdom?.RulingClan == null) return "That kingdom has no ruling clan.";

            try
            {
                var decision = new KingdomPolicyDecision(kingdom.RulingClan, DefaultPolicies.SacredMajesty, false);
                var outcomes = new MBList<DecisionOutcome>();
                foreach (var outcome in decision.DetermineInitialCandidates()) outcomes.Add(outcome);
                if (outcomes.Count < 2) return "That decision offered fewer than two outcomes.";

                var sb = new StringBuilder();
                sb.AppendLine("Mock vote in " + kingdom.Name + " on enacting Sacred Majesty."
                              + " Nothing is applied.");

                var redirected = 0;
                for (var i = 0; i < kingdom.Clans.Count; i++)
                {
                    var clan = kingdom.Clans[i];
                    if (!Court.IsMember(clan) || clan == kingdom.RulingClan) continue;

                    // What the clan wants on its own: the outcome it supports most.
                    DecisionOutcome own = null;
                    var bestOwn = float.MinValue;
                    for (var o = 0; o < outcomes.Count; o++)
                    {
                        var support = decision.DetermineSupport(clan, outcomes[o]);
                        if (support <= bestOwn) continue;
                        bestOwn = support;
                        own = outcomes[o];
                    }

                    // What it actually votes: this runs through the patched funnel.
                    var cast = decision.DetermineSupportOption(new Supporter(clan), outcomes,
                        out _, false);

                    var bloc = BlocModel.BlocOf(state, clan);
                    var moved = own != null && cast != null && own != cast;
                    if (moved) redirected++;

                    sb.AppendLine("    " + clan.Name
                                  + "  bloc " + (bloc == null ? "none" : bloc.Agenda.ToString())
                                  + (bloc?.Leader == clan ? " (leader)" : "")
                                  + ", loyalty " + LoyaltyModel.Of(state, clan).ToString("0.0")
                                  + "  | alone: " + Describe(own)
                                  + "  | votes: " + Describe(cast)
                                  + (moved ? "   <- BLOC OVERRODE IT" : ""));
                }

                sb.AppendLine(redirected + " clan(s) voted against their own preference because"
                              + " their bloc leader wanted otherwise.");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "Mock vote failed: " + ex.Message;
            }
        }

        private static string Describe(DecisionOutcome outcome)
        {
            if (outcome == null) return "?";
            var text = outcome.GetDecisionTitle();
            return text == null ? outcome.ToString() : text.ToString();
        }

        /// <summary>
        /// Standing claims to a throne, and what the Pretenders bloc needs to form.
        /// Usage: diplomacy.pretenders
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("pretenders", "diplomacy")]
        public static string Pretenders(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var sb = new StringBuilder();

            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated) continue;

                var claims = SuccessionModel.PretendersTo(state, kingdom);
                var weak = LegitimacyRegistry.IsWeak(state, kingdom);
                if (claims.Count == 0 && !weak) continue;

                sb.AppendLine(kingdom.Name + "  legitimacy "
                              + LegitimacyRegistry.Of(state, kingdom).ToString("0.0")
                              + (weak ? " (weak)" : " (not weak - no bloc can form)")
                              + ", " + claims.Count + " standing claim(s)");
                for (var i = 0; i < claims.Count; i++)
                    sb.AppendLine("    " + claims[i]);
            }

            // Who would stand if the throne fell vacant today. Printed because the claimant
            // rule has already been re-tuned twice against real courts rather than guessed at:
            // a flat 15% share admitted nobody, since a nine-clan court averages 11% each.
            sb.AppendLine();
            sb.AppendLine("-- who would stand at the next succession --");
            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated || kingdom.RulingClan == null) continue;

                var standing = 0;
                var lines = new StringBuilder();
                for (var i = 0; i < kingdom.Clans.Count; i++)
                {
                    var clan = kingdom.Clans[i];
                    if (!Court.IsMember(clan) || clan == kingdom.RulingClan) continue;

                    var ratio = SuccessionModel.InfluenceRatio(clan, kingdom);
                    var loyalty = LoyaltyModel.Of(state, clan);
                    var qualifies = SuccessionModel.HasPowerClaim(state, clan, kingdom);
                    if (qualifies) standing++;

                    lines.AppendLine("        " + clan.Name
                                     + "  influence x" + ratio.ToString("0.00")
                                     + " (needs x" + IntrigueConstants.SuccessionClaimantInfluenceRatio.ToString("0.00") + ")"
                                     + ", loyalty " + loyalty.ToString("0.0")
                                     + " (needs < " + IntrigueConstants.LoyaltyTransactional.ToString("0") + ")"
                                     + (qualifies ? "   <- WOULD STAND" : ""));
                }
                sb.AppendLine("    " + kingdom.Name + ": " + standing + " would stand on strength");
                sb.Append(lines);
            }

            sb.AppendLine("The Pretenders bloc needs BOTH a weak crown and a living claimant.");
            return sb.ToString();
        }

        /// <summary>
        /// Selects a clan in the open Court tab, through the same method a row click calls.
        /// Test-only: exists because the GABS bridge cannot click a Court row (every clan name
        /// also appears, earlier in the widget tree, in vanilla's hidden Clans list).
        /// Usage: diplomacy.test_court_select Harfit
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_court_select", "diplomacy")]
        public static string TestCourtSelect(List<string> args)
        {
            var court = UI.KingdomScreen.DiCourtVM.Current;
            if (court == null) return "The Kingdom screen is not open.";
            if (args == null || args.Count == 0) return "Usage: diplomacy.test_court_select <clan>";
            return court.SelectByName(string.Join(" ", args));
        }

        /// <summary>
        /// Crown legitimacy per kingdom, with what last moved it.
        /// Usage: diplomacy.legitimacy
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("legitimacy", "diplomacy")]
        public static string Legitimacy(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var sb = new StringBuilder();
            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated) continue;

                var value = LegitimacyRegistry.Of(state, kingdom);
                var weak = LegitimacyRegistry.IsWeak(state, kingdom);

                KingdomLegitimacy record = null;
                for (var i = 0; i < state.Legitimacy.Count; i++)
                    if (state.Legitimacy[i].Kingdom == kingdom) record = state.Legitimacy[i];

                sb.AppendLine(kingdom.Name + "  " + value.ToString("0.0")
                              + (weak ? "  WEAK - a pretender could speak openly" : "")
                              + (record == null
                                  ? "   (no record yet; reads as the starting value)"
                                  : "   last: " + record.LastReason));
            }

            sb.AppendLine("Starts at " + IntrigueConstants.LegitimacyStart.ToString("0")
                          + "; below " + IntrigueConstants.LegitimacyPretenderThreshold.ToString("0")
                          + " a crown is weak enough for a pretender (the claimant half of that"
                          + " condition is 2.5 and does not exist yet).");
            sb.AppendLine("The peace dividend cannot be driven by diplomacy.tick_days - it is"
                          + " measured in dates, and the clock does not move there.");
            return sb.ToString();
        }

        /// <summary>
        /// The court split into blocs: who leads each, what it weighs, and how much of that
        /// weight will actually vote its agenda rather than follow the ruler.
        /// Usage: diplomacy.blocs   or   diplomacy.blocs Khuzait
        /// With a kingdom named, also prints each clan's agenda pressures term by term.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("blocs", "diplomacy")]
        public static string Blocs(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            Kingdom filter = null;
            if (args != null && args.Count > 0)
            {
                var wanted = string.Join(" ", args);
                filter = FindKingdom(wanted);
                if (filter == null) return "No kingdom matching \"" + wanted + "\".";
            }

            var sb = new StringBuilder();
            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated) continue;
                if (filter != null && kingdom != filter) continue;

                var blocs = BlocModel.BlocsOf(state, kingdom);
                sb.AppendLine(kingdom.Name + "  (crown authority "
                              + CrownAuthority.Of(kingdom).ToString("+0.00;-0.00;0.00")
                              + ", ruler " + (kingdom.RulingClan == null ? "?" : kingdom.RulingClan.Name.ToString()) + ")");

                if (blocs.Count == 0)
                {
                    sb.AppendLine("    no bloc has formed - no agenda is pulling on anyone.");
                    continue;
                }

                var totalInfluence = 0f;
                for (var i = 0; i < blocs.Count; i++) totalInfluence += blocs[i].Power;

                for (var i = 0; i < blocs.Count; i++)
                {
                    var bloc = blocs[i];
                    sb.AppendLine("    " + bloc + "  ["
                                  + (bloc.PowerShare(totalInfluence) * 100f).ToString("0") + "% of the court]");
                    for (var m = 0; m < bloc.Members.Count; m++)
                        sb.AppendLine("        " + bloc.Members[m].Name
                                      + "  influence " + bloc.Members[m].Influence.ToString("0")
                                      + ", loyalty " + LoyaltyModel.Of(state, bloc.Members[m]).ToString("0.0"));
                }

                if (filter == null) continue;

                sb.AppendLine("    -- why each clan sits where it does --");
                for (var c = 0; c < kingdom.Clans.Count; c++)
                {
                    var clan = kingdom.Clans[c];
                    if (clan == null || clan.IsEliminated) continue;
                    if (clan.IsUnderMercenaryService)
                    {
                        sb.AppendLine("        " + clan.Name + ": a mercenary company, no seat at court");
                        continue;
                    }

                    var pressures = BlocModel.Pressures(state, clan);
                    if (pressures.Count == 0)
                    {
                        sb.AppendLine("        " + clan.Name + ": the ruling clan, no agenda of its own");
                        continue;
                    }

                    var line = new StringBuilder();
                    foreach (var pair in pressures)
                    {
                        if (line.Length > 0) line.Append(", ");
                        line.Append(pair.Key).Append(' ').Append(pair.Value.ToString("0.0"));
                    }
                    sb.AppendLine("        " + clan.Name + ": " + line);
                }
            }

            sb.AppendLine("Blocs are derived, never saved. Pretenders needs BOTH a crown below "
                          + IntrigueConstants.LegitimacyPretenderThreshold.ToString("0")
                          + " legitimacy and a living claimant; diplomacy.pretenders shows both.");
            return sb.ToString();
        }

        /// <summary>
        /// Every clan's loyalty to its own ruler, term by term. Sorted lowest first, because
        /// the bottom of the list is what decides whether a court fractures.
        /// Usage: diplomacy.loyalty   or   diplomacy.loyalty Khuzait
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("loyalty", "diplomacy")]
        public static string Loyalty(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            Kingdom filter = null;
            if (args != null && args.Count > 0)
            {
                var wanted = string.Join(" ", args);
                filter = FindKingdom(wanted);
                if (filter == null) return "No kingdom matching \"" + wanted + "\".";
            }

            var rows = new List<KeyValuePair<Clan, LoyaltyBreakdown>>();
            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated) continue;
                if (filter != null && kingdom != filter) continue;

                for (var i = 0; i < kingdom.Clans.Count; i++)
                {
                    var clan = kingdom.Clans[i];
                    if (!Court.IsMember(clan)) continue;

                    var explained = LoyaltyModel.Explain(state, clan);
                    if (!explained.Applies) continue;   // the ruling clan itself
                    rows.Add(new KeyValuePair<Clan, LoyaltyBreakdown>(clan, explained));
                }
            }

            if (rows.Count == 0) return "No clans to report on.";
            rows.Sort((a, b) => a.Value.Total.CompareTo(b.Value.Total));

            var sb = new StringBuilder();
            for (var i = 0; i < rows.Count; i++)
            {
                var clan = rows[i].Key;
                var e = rows[i].Value;
                var band = LoyaltyModel.Band(e.Total);
                sb.AppendLine(clan.Name + " (" + clan.Kingdom.Name + ")  " + e
                              + "  -> " + LoyaltyModel.Describe(band));
            }
            sb.AppendLine("Bands: >=" + IntrigueConstants.LoyaltyReliable.ToString("0")
                          + " reliable, >=" + IntrigueConstants.LoyaltyTransactional.ToString("0")
                          + " transactional, >=" + IntrigueConstants.LoyaltyDisaffected.ToString("0")
                          + " disaffected, below that a defection risk.");
            sb.AppendLine("Loyalty is derived, never saved. The crown-legitimacy term reads the"
                          + " real pool as of 2.4; it is zero only when a crown sits at the"
                          + " midpoint of the scale.");
            return sb.ToString();
        }

        /// <summary>
        /// The court's memory: who holds what against whom, and what it still weighs after
        /// decay. Grouped by the clan that feels wronged, heaviest first.
        /// Usage: diplomacy.grievances   or   diplomacy.grievances Vlandia
        /// A kingdom name filters to the clans of that kingdom.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("grievances", "diplomacy")]
        public static string Grievances(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            if (state.Grievances.Count == 0)
                return "No grievances on record. They accrue when a fief goes to a rival, when a"
                       + " war the clan opposed is declared, when the realm pays tribute, and from"
                       + " the other sources in design 02 §1.";

            Kingdom filter = null;
            if (args != null && args.Count > 0)
            {
                var wanted = string.Join(" ", args);
                filter = FindKingdom(wanted);
                if (filter == null) return "No kingdom matching \"" + wanted + "\".";
            }

            var byHolder = new Dictionary<Clan, List<Grievance>>();
            for (var i = 0; i < state.Grievances.Count; i++)
            {
                var g = state.Grievances[i];
                if (filter != null && g.Holder.Kingdom != filter) continue;
                if (!byHolder.TryGetValue(g.Holder, out var list))
                {
                    list = new List<Grievance>();
                    byHolder[g.Holder] = list;
                }
                list.Add(g);
            }

            if (byHolder.Count == 0) return "No grievances held by clans of that kingdom.";

            var sb = new StringBuilder();
            foreach (var pair in byHolder)
            {
                var holder = pair.Key;
                var crown = GrievanceRegistry.AgainstCrown(state, holder);
                sb.AppendLine(holder.Name + " (" + (holder.Kingdom == null ? "no kingdom" : holder.Kingdom.Name.ToString())
                              + ") - against its crown: " + crown.ToString("0.0"));

                pair.Value.Sort((a, b) => b.Weight.CompareTo(a.Weight));
                for (var i = 0; i < pair.Value.Count; i++)
                {
                    var g = pair.Value[i];
                    sb.AppendLine("    " + g.Weight.ToString("0.0").PadLeft(5) + "  " + g.Type
                                  + "  vs " + g.Target.Name
                                  + "  (" + g.Created.ElapsedDaysUntilNow.ToString("0") + "d ago)");
                }
            }
            sb.AppendLine("Decay is " + IntrigueConstants.GrievanceDecayPerDay.ToString("0.00")
                          + "/day; a record is dropped at zero.");
            return sb.ToString();
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
                       + "  terms: white, prisoners, indemnity=5000, tribute=800, fief=<name>,"
                       + " vassalage, dissolve";

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

            // Both sides, so the command mirrors what the AI actually requires. Checking only
            // the loser is what let the concession ladder sit unreachable for 13 in-game
            // years, and a diagnostic that asks a weaker question than the system it tests
            // will hide the same class of bug again.
            if (!PeaceTable.BothWouldSign(state, war, terms, out var refused))
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
                if (string.Equals(token, "dissolve", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token, "break_sphere", StringComparison.OrdinalIgnoreCase))
                {
                    terms.DissolveHegemony = true;
                    continue;
                }
                if (string.Equals(token, "vassalage", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token, "submit", StringComparison.OrdinalIgnoreCase))
                {
                    terms.ImposeVassalage = true;
                    if (terms.TributePerPeriod <= 0)
                        terms.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
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

        /// <summary>
        /// One day of everything the campaign's own daily handlers call (TreatyBehavior,
        /// CoreBehavior, WarExhaustionBehavior, ClaimsBehavior) - shared by tick_days and
        /// ai_week so the two cannot drift apart again.
        ///
        /// Each used to carry its own partial list. tick_days drove only exhaustion and
        /// claims, and a vassalage's Hold sat unchanged through 20 simulated days looking
        /// like a bug in the drift; ai_week kept a partial list until the run-06 review, so
        /// under it Hold never moved, treaties never paid their dividends and trust never
        /// decayed. The order follows the handlers, but the engine does not promise the
        /// order handlers fire in (CLAUDE.md §1), so nothing here may depend on it.
        /// </summary>
        private static void RunDailyUpkeep(ModState state)
        {
            Power.DailySample(state);
            WarExhaustion.DailyTick(state);
            Hegemony.DailyTick(state);
            TreatyRegistry.ExpireAndReward(state);
            TreatyRegistry.PayDueTribute(state);
            TreatyRegistry.PayPeaceDividends(state);
            TrustRegistry.DailyTick(state);
            ClaimRegistry.ExpireStale(state);
            ClaimRegistry.ResolveFabrications(state);
            GrievanceRegistry.DailyTick(state);

            // Included although a frozen clock means it can never actually pay: the rule is
            // that this drives the *full* daily set, and a list that quietly omits a call is
            // how diplomacy.tick_days once made a vassalage Hold look broken for a day.
            LegitimacyRegistry.DailyTick(state);

            // The succession watch belongs here for the same reason. It was left out of the
            // first version of this list and diplomacy.tick_days then reported nothing after a
            // ruler was killed, which read as a broken system and was a missing line.
            SuccessionModel.DailyWatch(state);
            SuccessionModel.RetireSpentClaims(state);
        }

        private static readonly char[] CommaSeparator = { ',' };
        private static readonly char[] EqualsSeparator = { '=' };

        /// <summary>
        /// Runs one week of AI diplomacy for every kingdom at once, and reports what each
        /// one did. Drives the same AiDiplomacy.Evaluate the weekly tick calls.
        ///
        /// This is the balance-pass tool: the daily tick spreads kingdoms across seven
        /// slots, which is right for play and useless for measuring. Repeat it to watch a
        /// decade of diplomacy in a minute.
        /// Usage: diplomacy.ai_week [number of weeks]
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("ai_week", "diplomacy")]
        public static string AiWeek(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var weeks = 1;
            if (args != null && args.Count > 0 && int.TryParse(args[0], out var parsed))
                weeks = parsed < 1 ? 1 : (parsed > 200 ? 200 : parsed);

            var counts = new Dictionary<AiDiplomacy.Move, int>();
            var sb = new StringBuilder();

            for (var week = 0; week < weeks; week++)
            {
                // A week is seven days. Without this the upkeep never runs, so weariness
                // never decays and claims never expire, and every kingdom stays
                // permanently discouraged by wars it finished years ago.
                for (var day = 0; day < 7; day++) RunDailyUpkeep(state);

                foreach (var kingdom in Kingdom.All)
                {
                    if (kingdom.IsEliminated) continue;
                    if (Hero.MainHero != null && kingdom.Leader == Hero.MainHero) continue;

                    var move = AiDiplomacy.Evaluate(state, kingdom);
                    counts.TryGetValue(move, out var n);
                    counts[move] = n + 1;

                    if (weeks == 1 && move != AiDiplomacy.Move.None)
                        sb.AppendLine("  " + kingdom.Name + ": " + move);
                }
            }

            var header = new StringBuilder();
            header.AppendLine("Ran " + weeks + " week(s) of AI diplomacy, with "
                              + (weeks * 7) + " days of upkeep. Campaign time unchanged.");
            if (weeks > 4)
            {
                // Say what this cannot show, or a long quiet run reads as a finding.
                header.AppendLine("NOTE: the campaign clock does not move here, so treaties never");
                header.AppendLine("expire and clan influence never regenerates. A long run will");
                header.AppendLine("therefore under-report wars. Use it for single decisions and for");
                header.AppendLine("exhaustion rates, not for the long-run war/peace rhythm.");
            }
            foreach (var pair in counts)
                header.AppendLine("  " + pair.Key + ": " + pair.Value);
            header.Append(sb);
            return header.ToString();
        }

        /// <summary>
        /// Shows how much each side values an agreement with the other - the same numbers
        /// the AI and the diplomacy menu read.
        /// Usage: diplomacy.pact_value Vlandia | Battania
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("pact_value", "diplomacy")]
        public static string PactValue(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.pact_value <kingdom> | <kingdom>";

            var a = FindKingdom(parts[0]);
            var b = FindKingdom(parts[1]);
            if (a == null || b == null) return "Kingdom not found.";

            var sb = new StringBuilder();
            sb.AppendLine(a.Name + " values an agreement with " + b.Name + " at "
                          + AiDiplomacy.PactValue(state, a, b).ToString("0.0"));
            sb.AppendLine(b.Name + " values an agreement with " + a.Name + " at "
                          + AiDiplomacy.PactValue(state, b, a).ToString("0.0"));

            sb.AppendLine("Ambition, subtracted above: " + a.Name + " "
                          + (Power.Ambition(a) * DiplomacyConstants.PactWeightAmbition).ToString("0.0")
                          + ", " + b.Name + " "
                          + (Power.Ambition(b) * DiplomacyConstants.PactWeightAmbition).ToString("0.0"));

            var pull = AiDiplomacy.BalancingPull(state, a, b, out var against);
            sb.AppendLine("Balancing pull, included above: "
                          + (against == null
                              ? "none - no sphere outweighs the two of them"
                              : (pull * DiplomacyConstants.PactWeightBalancing).ToString("0.0")
                                + " against " + against.Name + "'s sphere ("
                                + Power.SmoothedSphere(state, against).ToString("0") + " smoothed strength)"));
            sb.AppendLine("Thresholds: non-aggression " + DiplomacyConstants.AiNonAggressionThreshold.ToString("0")
                          + ", defensive pact " + DiplomacyConstants.AiDefensivePactThreshold.ToString("0")
                          + ", alliance " + DiplomacyConstants.AiAllianceThreshold.ToString("0"));
            sb.AppendLine("The lower of the two decides: both sides have to want it.");
            return sb.ToString();
        }

        /// <summary>
        /// Opens the diplomacy menu - the same entry point as Ctrl+D on the map. Useful
        /// when a key is bound to something else, and the only way to reach the menu from
        /// a script.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("menu", "diplomacy")]
        public static string Menu(List<string> args)
        {
            if (CoreBehavior.State == null) return NoCampaign;
            UI.DiplomacyMenu.Open();
            return "Diplomacy menu opened.";
        }

        /// <summary>
        /// Why a kingdom is or is not going to war with another, term by term.
        /// Usage: diplomacy.war_value Aserai | Khuzait
        /// With one kingdom named, reports it against every other.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("war_value", "diplomacy")]
        public static string WarValue(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 1) return "Usage: diplomacy.war_value <kingdom> [| <kingdom>]";

            var a = FindKingdom(parts[0]);
            if (a == null) return "No kingdom matching \"" + parts[0] + "\".";

            if (parts.Count >= 2)
            {
                var b = FindKingdom(parts[1]);
                if (b == null) return "No kingdom matching \"" + parts[1] + "\".";
                return AiDiplomacy.ExplainWarValue(state, a, b);
            }

            var sb = new StringBuilder();
            foreach (var other in Kingdom.All)
            {
                if (other == a || other.IsEliminated) continue;
                sb.AppendLine(AiDiplomacy.ExplainWarValue(state, a, other));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Prints the exhaustion bands with their edges and what each one means.
        ///
        /// This is how a rival's war exhaustion is shown to the player - a band, never a
        /// figure, with the exact value reserved for Phase 3 espionage. The edges are read
        /// from the same constants the AI uses, so this also verifies they have not drifted.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("bands", "diplomacy")]
        public static string Bands(List<string> args)
        {
            var sb = new StringBuilder();
            sb.AppendLine("How a rival's war exhaustion appears to us:");
            sb.AppendLine();

            var probes = new[] { 0f, 19f, 20f, 39f, 40f, 59f, 60f, 79f, 80f, 100f };
            var lastBand = (ExhaustionBand)(-1);

            for (var i = 0; i < probes.Length; i++)
            {
                var band = ExhaustionBands.Of(probes[i]);
                if (band == lastBand) continue;
                lastBand = band;

                sb.AppendLine(ExhaustionBands.Bar(band) + " " + ExhaustionBands.Name(band)
                              + "  from " + probes[i].ToString("0"));
                sb.AppendLine("      " + ExhaustionBands.Meaning(band));
            }

            sb.AppendLine();
            sb.AppendLine("Edges come from the behavioural thresholds, not arbitrary fifths:");
            sb.AppendLine("  court pressure   " + DiplomacyConstants.ExhaustionCourtPressure.ToString("0"));
            sb.AppendLine("  seeks peace      " + DiplomacyConstants.ExhaustionSeekPeace.ToString("0"));
            sb.AppendLine("  accepts bad terms " + DiplomacyConstants.ExhaustionAcceptBadTerms.ToString("0"));
            return sb.ToString();
        }

        /// <summary>
        /// Writes a telemetry snapshot to the log and a full report to file, on demand.
        /// The weekly snapshot happens on its own; this is for grabbing one at a moment of
        /// interest, and for checking both writers work.
        /// </summary>
        /// <summary>
        /// Every sphere on the map, with the hold on each link and what is pulling it.
        /// Usage: diplomacy.hegemony
        /// </summary>
        /// <summary>
        /// What submitting to a patron is worth to a kingdom, term by term.
        /// Usage: diplomacy.submission_value Sturgia | Khuzait
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("submission_value", "diplomacy")]
        public static string SubmissionValueCommand(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.submission_value <candidate> | <patron>";

            var candidate = FindKingdom(parts[0]);
            var patron = FindKingdom(parts[1]);
            if (candidate == null) return "No kingdom matching \"" + parts[0] + "\".";
            if (patron == null) return "No kingdom matching \"" + parts[1] + "\".";

            var value = Hegemony.SubmissionValue(state, candidate, patron, out var explanation);

            var sb = new StringBuilder();
            sb.AppendLine(candidate.Name + " considering submission to " + patron.Name);
            sb.AppendLine("  " + explanation);
            sb.AppendLine("  verdict: " + (value >= DiplomacyConstants.AiSubmissionThreshold
                ? "would submit"
                : "would not submit"));

            // settlesWar exactly as AiDiplomacy.TrySubmit passes it. Without this the command
            // answered "Make peace first" for every patron that is one of the candidate's
            // attackers - which is the whole of the route added in design/04 §12.4.4, so the
            // one diagnostic pointed at the new branch reported it as impossible.
            var settlesWar = patron.IsAtWarWith(candidate);
            if (!TreatyRegistry.CanSign(state, patron, candidate, Models.TreatyType.Vassalage,
                    out var why, settlesWar: settlesWar))
                sb.AppendLine("  but it could not be signed: " + why);
            else if (settlesWar)
                sb.AppendLine("  signing it would end their war.");

            return sb.ToString();
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("hegemony", "diplomacy")]
        public static string HegemonyCommand(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var links = new List<Models.Treaty>();
            Hegemony.CollectLinks(state, links);
            if (links.Count == 0)
                return "Nobody holds a vassal. There are no hegemons - submission is imposed at"
                       + " a peace table above war score "
                       + Diplomacy.PeaceTable.SubjugationCost.ToString("0")
                       + ", or offered voluntarily at submission value "
                       + DiplomacyConstants.AiSubmissionThreshold.ToString("0") + ".";

            var sb = new StringBuilder();
            sb.Append("Hegemons: ").Append(Hegemony.CountHegemons(state))
              .Append("   links: ").Append(links.Count).AppendLine();

            foreach (var patron in Kingdom.All)
            {
                if (patron.IsEliminated || !Hegemony.IsHegemon(state, patron)) continue;

                var held = new List<Models.Treaty>();
                Hegemony.CollectVassalages(state, patron, held);
                sb.AppendLine();
                sb.Append(patron.Name).Append(" holds ").Append(held.Count).AppendLine(" vassal(s):");

                for (var i = 0; i < held.Count; i++)
                {
                    var link = held[i];
                    var hold = Hegemony.HoldOf(link);
                    Hegemony.HoldTarget(state, link, out var explanation);

                    sb.Append("  ").Append(link.SubordinateParty.Name)
                      .Append("  hold ").Append(hold.ToString("0.0"))
                      .Append("  marks ").Append(link.DefianceMarks)
                      .Append("  tribute ").Append(link.TributeAmount)
                      .Append("  until ").Append(link.ExpiresOn).AppendLine();
                    sb.Append("      ").AppendLine(explanation);
                    sb.Append("      ").Append(Describe(state, link))
                      .Append("   (revolts below ").Append(Hegemony.SecessionThreshold(state, link).ToString("0.0"))
                      .AppendLine(")");
                }
            }

            return sb.ToString();
        }

        private static string Describe(ModState state, Models.Treaty link)
        {
            var hold = Hegemony.HoldOf(link);
            if (hold >= DiplomacyConstants.HoldRenewThreshold) return "loyal - will renew willingly";
            if (hold >= DiplomacyConstants.HoldPassiveResistanceThreshold) return "serving, but will let the term lapse";
            if (hold >= DiplomacyConstants.HoldDefianceThreshold) return "resisting - refuses summons, withholds tribute";
            if (!Hegemony.IsAtBreakingPoint(state, link)) return "defiant - will treat with outsiders";
            return "at breaking point - counting down to revolt";
        }

        /// <summary>
        /// Every kingdom's strength as the diplomacy formulas read it: the engine's live
        /// military figure, its share of the world, its fortifications, and the sphere it
        /// belongs to. Written because the lead found, reading a save by hand, that the
        /// hegemon of the whole map was nearly its weakest kingdom - a fact no command could
        /// show.
        /// Usage: diplomacy.strength
        /// </summary>
        /// <summary>
        /// Sets a kingdom's smoothed strength, for testing greed and dread without waiting a
        /// year of campaign for a kingdom to grow into them. It moves only the average: live
        /// strength, the armies and every other system are untouched, and each daily sample
        /// pulls the figure back toward the live one. Never use it in a save you mean to keep.
        /// Usage: diplomacy.set_smoothed_strength Northern Empire | 60000
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("set_smoothed_strength", "diplomacy")]
        public static string SetSmoothedStrength(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2 || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value) || value < 0f)
                return "Usage: diplomacy.set_smoothed_strength <kingdom> | <strength>";

            var kingdom = FindKingdom(parts[0]);
            if (kingdom == null) return "No kingdom matching \"" + parts[0] + "\".";

            state.PowerRecords.RemoveAll(r => r.Kingdom == kingdom);
            state.PowerRecords.Add(new KingdomPower(kingdom, value));

            return kingdom.Name + " smoothed strength set to " + value.ToString("0")
                   + ": smoothed dominance " + Power.SmoothedDominance(state, kingdom).ToString("0.00")
                   + ", greed " + Power.Greed(state, kingdom).ToString("0.00") + ". Test only - do not save.";
        }

        /// <summary>
        /// Sets the player hero's age and cures the old-age illness, for keeping a test
        /// character alive through a long balance run. Written during run 06, when the test
        /// save's 75-year-old hero with no heir was dying of old age - which ends the game and
        /// stalls the run. The alternative, turning off the campaign's life and death cycle,
        /// would have stopped every AI ruler dying too and changed what the run measures.
        /// Touches nobody else.
        ///
        /// Age alone is not enough, as the first attempt found: the engine does not kill the
        /// main hero outright but makes it ill (AgingCampaignBehavior, verified by IL in
        /// v1.4.8), and an illness already under way drains hit points until death whatever
        /// the age. Illness is <c>Campaign.MainHeroIllDays != -1</c>.
        /// Usage: diplomacy.test_set_player_age 35
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_set_player_age", "diplomacy")]
        public static string TestSetPlayerAge(List<string> args)
        {
            if (Campaign.Current == null || Hero.MainHero == null) return NoCampaign;
            if (args == null || args.Count == 0 || !int.TryParse(args[0], out var years) || years < 18 || years > 100)
                return "Usage: diplomacy.test_set_player_age <18-100>";

            var wasIll = Hero.IsMainHeroIll;
            Hero.MainHero.SetBirthDay(CampaignTime.YearsFromNow(-years));
            Campaign.Current.MainHeroIllDays = -1;
            Hero.MainHero.HitPoints = Hero.MainHero.MaxHitPoints;

            return Hero.MainHero.Name + " is now " + Hero.MainHero.Age.ToString("0")
                   + (wasIll ? ", cured of the illness" : "")
                   + ", at full health. Test characters only.";
        }

        /// <summary>
        /// Raises the campaign's fast-forward multiplier, so an unattended balance run covers
        /// in-game years in a sitting rather than a day.
        ///
        /// The game's own ceiling is <c>UnstoppableFastForward</c>, which the bridge can already
        /// select; what it cannot reach is <see cref="Campaign.SpeedUpMultiplier"/>, the factor
        /// that mode is multiplied by. There is no vanilla console command for it (checked:
        /// neither <c>campaign.set_speed_up_multiplier</c> nor <c>campaign.set_campaign_speed</c>
        /// exists in v1.4.8), and no GABP tool exposes it.
        ///
        /// **Test only, and it distorts what it measures.** Everything the campaign does per
        /// tick still happens, but the wall-clock budget per tick shrinks, so a machine that
        /// cannot keep up drops frames rather than slowing the clock - battles resolve on the
        /// map at a different rate from a played game. Use it to reach a world state, not to
        /// measure how fast one arrives. Capped at 50 so a typo cannot wedge the session.
        /// Usage: diplomacy.test_set_speed 10
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_set_speed", "diplomacy")]
        public static string TestSetSpeed(List<string> args)
        {
            if (Campaign.Current == null) return NoCampaign;

            var before = Campaign.Current.SpeedUpMultiplier;
            if (args == null || args.Count == 0)
                return "Current speed-up multiplier: " + before.ToString("0.##")
                       + ", time control mode: " + Campaign.Current.TimeControlMode
                       + ". Usage: diplomacy.test_set_speed <1-50>";

            if (!float.TryParse(args[0], out var multiplier) || multiplier < 1f || multiplier > 50f)
                return "Usage: diplomacy.test_set_speed <1-50>";

            Campaign.Current.SpeedUpMultiplier = multiplier;
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.UnstoppableFastForward;

            return "Speed-up multiplier " + before.ToString("0.##") + " -> "
                   + Campaign.Current.SpeedUpMultiplier.ToString("0.##")
                   + ", mode " + Campaign.Current.TimeControlMode
                   + ". Test sessions only - this changes how much wall clock a tick gets,"
                   + " not what the tick does.";
        }

        /// <summary>
        /// Opens the Kingdom screen the way the K key does - a KingdomState pushed onto the
        /// game state manager. Test only: it exists because the GABP bridge cannot send a
        /// keypress and indexes no map-bar widgets, so a UI pass on the Kingdom screen
        /// otherwise needs a human hand on the keyboard.
        ///
        /// Vanilla only ever offers this screen to a player already in a kingdom - the K
        /// key is bound to nothing otherwise. Pushing it anyway for an independent clan hit
        /// a NullReferenceException inside KingdomState's own construction (2026-09-21):
        /// caught here, but the state had already half-registered its native UI stack, and
        /// the game crashed several minutes later (CLR20r3 / access violation) on a later
        /// frame that assumed it. The guard below is the fix - refuse before touching
        /// GameStateManager at all, rather than trusting the try/catch to make it safe.
        /// Usage: diplomacy.test_open_kingdom
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_open_kingdom", "diplomacy")]
        public static string TestOpenKingdom(List<string> args)
        {
            if (Campaign.Current == null) return NoCampaign;
            if (Clan.PlayerClan?.Kingdom == null)
                return "The player is not in a kingdom - vanilla never offers this screen here, "
                       + "and pushing it anyway has crashed the game. Load onto a kingdom member first.";

            try
            {
                var manager = GameStateManager.Current;
                if (manager == null) return "No game state manager - not in a running game.";
                manager.PushState(manager.CreateState<TaleWorlds.CampaignSystem.GameState.KingdomState>(), 0);
                return "Kingdom screen pushed.";
            }
            catch (Exception ex)
            {
                return "Could not open the Kingdom screen: " + ex.Message;
            }
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("strength", "diplomacy")]
        public static string StrengthCommand(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var kingdoms = new List<Kingdom>();
            var total = 0f;
            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom.IsEliminated) continue;
                kingdoms.Add(kingdom);
                total += kingdom.CurrentTotalStrength;
            }
            kingdoms.Sort((x, y) => y.CurrentTotalStrength.CompareTo(x.CurrentTotalStrength));

            var sb = new StringBuilder();
            sb.AppendLine("Live strength decides war now; smoothed (" + DiplomacyConstants.StrengthSmoothingDays.ToString("0")
                          + "-day average) decides greed and coalitions.");
            sb.AppendLine("rank  kingdom            strength   share  dom.  smoothed  s.dom  ambit  greed  fiefs  sphere");
            for (var i = 0; i < kingdoms.Count; i++)
            {
                var k = kingdoms[i];
                var fiefs = 0;
                for (var s = 0; s < k.Settlements.Count; s++)
                    if (k.Settlements[s].IsFortification) fiefs++;

                string sphere;
                var patron = Hegemony.PatronOf(state, k);
                if (patron != null)
                {
                    sphere = "vassal of " + patron.Name + " (balance vs patron "
                             + Power.Balance(k, patron).ToString("+0.00;-0.00") + ")";
                }
                else if (Hegemony.IsHegemon(state, k))
                {
                    sphere = "hegemon, " + Hegemony.VassalCount(state, k) + " vassal(s), sphere "
                             + Hegemony.SphereStrength(state, k).ToString("0");
                }
                else
                {
                    sphere = "free";
                }

                sb.Append((i + 1).ToString().PadLeft(4)).Append("  ")
                  .Append(k.Name.ToString().PadRight(18)).Append(' ')
                  .Append(k.CurrentTotalStrength.ToString("0").PadLeft(8)).Append(' ')
                  .Append((total <= 0f ? 0f : k.CurrentTotalStrength / total * 100f).ToString("0.0").PadLeft(6)).Append("% ")
                  .Append(Power.Dominance(k).ToString("0.00").PadLeft(5)).Append(' ')
                  .Append(Power.Smoothed(state, k).ToString("0").PadLeft(9)).Append(' ')
                  .Append(Power.SmoothedDominance(state, k).ToString("0.00").PadLeft(6)).Append(' ')
                  .Append(Power.Ambition(k).ToString("0.00").PadLeft(6)).Append(' ')
                  .Append(Power.Greed(state, k).ToString("0.00").PadLeft(6)).Append(' ')
                  .Append(fiefs.ToString().PadLeft(6)).Append("  ")
                  .AppendLine(sphere);
            }
            return sb.ToString();
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("report", "diplomacy")]
        public static string Report(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            Telemetry.WriteSnapshot(state);
            var path = Telemetry.WriteReport(state);
            return "Snapshot written to the log, full report written to:" + Environment.NewLine + path;
        }

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
