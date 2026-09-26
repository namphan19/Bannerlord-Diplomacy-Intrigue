using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Espionage;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using DiplomacyIntrigue.Statecraft;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
    public static partial class DebugCommands
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
                  + (Statecraft.StatecraftTerms.ExposureChance(kingdom, settlement.MapFaction as Kingdom) * 100f).ToString("0") + "%.";
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
                if (!kingdom.IsRealm()) continue;

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
                if (!kingdom.IsRealm() || kingdom.RulingClan == null) continue;

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
        /// A court as its Encyclopedia page describes it - bands only for a rival, a pointer to
        /// the Court tab for your own. Prints the very view model the page binds, so it checks
        /// what the page will say rather than a second rendering of it. Put it beside
        /// <c>diplomacy.loyalty</c> and <c>diplomacy.legitimacy</c> to see each band against
        /// the figure behind it.
        /// Usage: diplomacy.court_bands [kingdom]
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("court_bands", "diplomacy")]
        public static string CourtBandsCommand(List<string> args)
        {
            if (Campaign.Current == null) return NoCampaign;

            Kingdom filter = null;
            if (args != null && args.Count > 0)
            {
                var wanted = string.Join(" ", args);
                filter = FindKingdom(wanted);
                if (filter == null) return "No kingdom named \"" + wanted + "\".";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Edges: crown Failing < " + IntrigueConstants.LegitimacyPretenderThreshold.ToString("0")
                          + " <= Questioned < " + IntrigueConstants.LegitimacyNeutral.ToString("0")
                          + " <= Secure; great house at x"
                          + IntrigueConstants.SuccessionClaimantInfluenceRatio.ToString("0.00")
                          + " the court's average influence; no weight at <= 0.");
            foreach (var kingdom in Kingdom.All)
            {
                if (filter != null && kingdom != filter) continue;
                if (filter == null && !kingdom.IsRealm()) continue;
                sb.AppendLine();
                sb.AppendLine("== " + kingdom.Name + " ==");
                sb.Append(new UI.EncyclopediaPages.DiEncyclopediaCourtVM(kingdom).Describe());
            }
            return sb.ToString();
        }

        /// <summary>
        /// Opens a kingdom's Encyclopedia page, the way clicking its link in a message does.
        /// Test-only: reaches a page in one step instead of clicking through the Encyclopedia's
        /// lists from a tool call.
        /// Usage: diplomacy.test_open_encyclopedia Sturgia
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_open_encyclopedia", "diplomacy")]
        public static string TestOpenEncyclopedia(List<string> args)
        {
            if (Campaign.Current == null) return NoCampaign;
            if (args == null || args.Count == 0) return "Usage: diplomacy.test_open_encyclopedia <kingdom>";

            var wanted = string.Join(" ", args);
            var kingdom = FindKingdom(wanted);
            if (kingdom == null) return "No kingdom named \"" + wanted + "\".";

            try
            {
                Campaign.Current.EncyclopediaManager.GoToLink(kingdom.EncyclopediaLink);
                return "Opened the Encyclopedia page of " + kingdom.Name + ".";
            }
            catch (Exception ex)
            {
                return "Could not open the Encyclopedia: " + ex.Message;
            }
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
                if (!kingdom.IsRealm()) continue;

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
                          + " a crown is weak enough for a pretender's party to gather, if a claim"
                          + " stands (diplomacy.pretenders); below " + IntrigueConstants.LegitimacyNeutral.ToString("0")
                          + " it costs every clan loyalty.");
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
                if (!kingdom.IsRealm()) continue;
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
                if (!kingdom.IsRealm()) continue;
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
                                  + "  (" + g.Created.ElapsedDaysUntilNow.ToString("0") + "d ago)"
                                  + (g.Answers > 0 ? "  answered x" + g.Answers + " " + g.AnsweredOn.ElapsedDaysUntilNow.ToString("0") + "d ago"
                                                     + (GrievanceRegistry.IsRemembered(g) ? ", remembered" : "") : ""));
                }
            }
            sb.AppendLine("Decay is " + IntrigueConstants.GrievanceDecayPerDay.ToString("0.00")
                          + "/day at a median steward, times the steward of the house each is held against (design 08 S-6)"
                          + "; a record is dropped at zero, unless the crown answered it inside "
                          + IntrigueConstants.AmendsMemoryYears.ToString("0") + " years (design 09).");
            return sb.ToString();
        }

        /// <summary>
        /// Design 09 C1, the diagnostic: every grievance held against one crown, priced term by term
        /// by the resolver the Court tab and the AI read, and what the AI would answer this week.
        /// A dry run: nothing is paid.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("amends", "diplomacy")]
        public static string AmendsQuote(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            var parts = SplitOnPipe(args);
            if (parts.Count < 1) return "Usage: diplomacy.amends <kingdom> [| <clan>]";

            var kingdom = FindKingdom(parts[0]);
            if (kingdom == null) return "No kingdom matching \"" + parts[0] + "\".";
            Clan only = null;
            if (parts.Count > 1)
            {
                only = FindClan(parts[1]);
                if (only == null) return "No clan matching \"" + parts[1] + "\".";
            }
            return Intrigue.Amends.Describe(state, kingdom, only);
        }

        /// <summary>
        /// Test lever: the clan's crown makes amends for its heaviest grievance against the crown (or
        /// the one of <c>type</c>), paying the real price through <see cref="Intrigue.Amends.Execute"/> -
        /// the same call the Court tab's button and the AI make. Whoever rules pays, player or AI.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_amends", "diplomacy")]
        public static string TestAmends(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            var parts = SplitOnPipe(args);
            if (parts.Count < 1) return "Usage: diplomacy.test_amends <clan> [| <grievance type>]";

            var clan = FindClan(parts[0]);
            if (clan == null) return "No clan matching \"" + parts[0] + "\".";
            var ruling = clan.Kingdom?.RulingClan;
            if (ruling == null) return clan.Name + " has no crown.";

            GrievanceType wanted = GrievanceType.None;
            if (parts.Count > 1 && !Enum.TryParse(parts[1], true, out wanted))
                return "Unknown grievance type \"" + parts[1] + "\". Types: " + string.Join(", ", Enum.GetNames(typeof(GrievanceType)));

            Grievance pick = null;
            foreach (var g in GrievanceRegistry.Of(state, clan))
            {
                if (g.Target != ruling) continue;
                if (wanted != GrievanceType.None && g.Type != wanted) continue;
                pick = g;   // Of() is heaviest first
                break;
            }
            if (pick == null) return clan.Name + " holds no such grievance against its crown.";

            var quote = Intrigue.Amends.QuoteFor(state, pick);
            if (!Intrigue.Amends.Execute(state, pick, out var failed))
                return "Refused: " + failed;
            return "Amends made to " + clan.Name + " for " + pick.Type + ": " + quote.Influence + " influence, "
                   + quote.Gold.ToString("N0") + " denars. Loyalty " + quote.LoyaltyBefore.ToString("0.0") + " -> "
                   + Intrigue.LoyaltyModel.Of(state, clan).ToString("0.0") + " (predicted " + quote.LoyaltyAfter.ToString("0.0") + ").";
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
        /// Ends a live treaty as if its term had run out, through the same
        /// <c>TreatyRegistry.Expire</c> the daily upkeep calls - trust dividend and all. For a
        /// test that needs a pact gone without the grudge a breach leaves, since
        /// <c>tick_days</c> cannot move the calendar that expiry reads. Test saves only.
        /// Usage: diplomacy.test_expire_treaty Khuzait | Northern Empire | TributaryPact
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_expire_treaty", "diplomacy")]
        public static string TestExpireTreaty(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 3)
                return "Usage: diplomacy.test_expire_treaty <kingdom> | <kingdom> | <type>";

            var a = FindKingdom(parts[0]);
            var b = FindKingdom(parts[1]);
            if (a == null) return "No kingdom matching \"" + parts[0] + "\".";
            if (b == null) return "No kingdom matching \"" + parts[1] + "\".";
            if (!Enum.TryParse(parts[2], true, out TreatyType type))
                return "Unknown treaty type \"" + parts[2] + "\".";

            var treaty = state.ActiveTreatyBetween(a, b, type);
            if (treaty == null) return "No active " + type + " between those kingdoms.";

            TreatyRegistry.Expire(state, treaty);
            return "Expired, honoured in full: " + treaty + ". Trust now " + a.Name + " -> " + b.Name + " "
                   + TrustRegistry.Get(state, a, b).ToString("0.0") + ", " + b.Name + " -> " + a.Name + " "
                   + TrustRegistry.Get(state, b, a).ToString("0.0") + ".";
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

            // Advances every internal war and looks for a new one. Exhaustion accrues here; the
            // cooldown after a war is measured in dates and, like the peace dividend, cannot.
            InternalWars.DailyTick(state);

            // Spy networks decay daily and lose a handler who no longer qualifies (Phase 3.1).
            SpyNetworks.DailyTick(state);

            // Operations resolve on a date, so under a frozen clock none comes due here; the call
            // is in the list because the rule is the full daily set. test_resolve_mission is the
            // lever that resolves one now.
            Missions.DailyTick(state);
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

                // The court's week - the grievance scan, the civil wars' side changes and the AI's
                // amends - through the campaign's own list, not a copy of it. The copy this used to
                // keep had already lost the grievance scan once (fixed 2026-09-25), the
                // partial-tick trap of CLAUDE.md §1.
                IntrigueUpkeep.Weekly(state);

                // The espionage week: counter-intelligence budgets paid, then networks paid for
                // and grown (Phase 3.1, 3.3) - the campaign's own list, not a copy of it.
                EspionageUpkeep.Weekly(state);

                foreach (var kingdom in Kingdom.All)
                {
                    if (!kingdom.IsRealm()) continue;
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
            sb.AppendLine("Asked by the other, with its envoy's persuasion (design 08 S-3): " + a.Name + " "
                          + AiDiplomacy.PactValueWhenAsked(state, a, b).ToString("0.0") + " ("
                          + StatecraftTerms.PersuasionLine(b) + "), " + b.Name + " "
                          + AiDiplomacy.PactValueWhenAsked(state, b, a).ToString("0.0") + " ("
                          + StatecraftTerms.PersuasionLine(a) + ")");

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
                if (other == a || !other.IsRealm()) continue;
                sb.AppendLine(AiDiplomacy.ExplainWarValue(state, a, other));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Whether a kingdom could demand tribute of another, gate by gate, with the target's
        /// court house by house - printed from <see cref="AiDiplomacy.EvaluateTribute"/>, the
        /// answer the weekly scan and the player's button both act on.
        /// Usage: diplomacy.tribute_value Vlandia | Battania
        /// With one kingdom named, reports it as the demander against every other.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("tribute_value", "diplomacy")]
        public static string TributeValue(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 1) return "Usage: diplomacy.tribute_value <demander> [| <target>]";

            var a = FindKingdom(parts[0]);
            if (a == null) return "No kingdom matching \"" + parts[0] + "\".";

            if (parts.Count >= 2)
            {
                var b = FindKingdom(parts[1]);
                if (b == null) return "No kingdom matching \"" + parts[1] + "\".";
                return AiDiplomacy.ExplainTributeValue(state, a, b);
            }

            var sb = new StringBuilder();
            foreach (var other in Kingdom.All)
            {
                if (other == a || !other.IsRealm()) continue;
                sb.AppendLine(AiDiplomacy.ExplainTributeValue(state, a, other));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Puts one demand for tribute through <see cref="AiDiplomacy.DemandTributeOf"/> now -
        /// the body the weekly scan runs for each target - so a demand on a player-ruled realm
        /// can be seen without waiting for the scan to pick it. Every gate still applies.
        /// Test saves only: an accepted demand signs a real pact.
        /// Usage: diplomacy.test_demand_tribute Vlandia | Battania
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_demand_tribute", "diplomacy")]
        public static string TestDemandTribute(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.test_demand_tribute <demander> | <target>";
            var a = FindKingdom(parts[0]);
            if (a == null) return "No kingdom matching \"" + parts[0] + "\".";
            var b = FindKingdom(parts[1]);
            if (b == null) return "No kingdom matching \"" + parts[1] + "\".";

            var before = AiDiplomacy.EvaluateTribute(state, a, b);
            var notAsked = AiDiplomacy.WhyPlayerNotAsked(state, a, b);
            if (!AiDiplomacy.DemandTributeOf(state, a, b))
                return "No demand made: " + (before.Blocked ?? notAsked ?? "Sign refused it - see the log.");
            return b.Leader == Hero.MainHero
                ? "Demand put to the player: answer the inquiry."
                : b.Name + " pays tribute to " + a.Name + " now.";
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
                if (!patron.IsRealm() || !Hegemony.IsHegemon(state, patron)) continue;

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

        /// <summary>
        /// Drives the Clan screen's Intelligence tab (Phase 3.7) through the same methods its
        /// buttons call, and prints what the tab binds. The bridge's clicks match widgets by text
        /// and can land on a hidden panel (UI-INTEGRATION.md 0b.5), so this is how a check says
        /// what it exercised: the view model's commands, not the click itself.
        /// Usage: diplomacy.test_intel open | show | select &lt;kingdom&gt; | mission &lt;type&gt; | plan | mark &lt;n&gt;
        ///        | send | close | budget +|- | recall | picker [kingdom] | pick &lt;realm or hero&gt; | post | cancel &lt;n&gt;
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_intel", "diplomacy")]
        public static string TestIntel(List<string> args)
        {
            if (Campaign.Current == null) return NoCampaign;
            var words = args ?? new List<string>();
            var verb = words.Count > 0 ? words[0].ToLowerInvariant() : "show";
            var rest = words.Count > 1 ? string.Join(" ", words.GetRange(1, words.Count - 1)) : string.Empty;

            try
            {
                if (verb == "open")
                {
                    var manager = GameStateManager.Current;
                    if (manager == null) return "No game state manager - not in a running game.";
                    manager.PushState(manager.CreateState<TaleWorlds.CampaignSystem.GameState.ClanState>(), 0);
                    var opened = UI.ClanScreen.DiIntelligenceVM.Current;
                    if (opened == null) return "Clan screen pushed, but the Intelligence tab's view model is not there - the mixin did not attach.";
                    opened.ExecuteShow();
                    return "Clan screen pushed, Intelligence tab shown.\n" + opened.Describe();
                }

                var vm = UI.ClanScreen.DiIntelligenceVM.Current;
                if (vm == null) return "The Clan screen is not open (diplomacy.test_intel open).";

                switch (verb)
                {
                    case "show": break;
                    case "select":
                        var k = FindKingdom(rest);
                        if (k == null) return "No kingdom \"" + rest + "\".";
                        vm.Select(k);
                        break;
                    case "mission":
                        if (!Enum.TryParse(rest, true, out SpyMissionType type)) return "No mission type \"" + rest + "\".";
                        vm.SelectMission(type);
                        break;
                    case "plan": vm.ExecutePlan(); break;
                    case "mark":
                        if (!int.TryParse(rest, out var index) || !vm.PickMark(index)) return "No mark " + rest + " on an open plan.";
                        break;
                    case "send": vm.ExecuteSend(); break;
                    case "close": vm.ExecuteClosePlan(); vm.ExecuteClosePicker(); break;
                    case "budget":
                        if (rest == "+") vm.ExecuteBudgetUp();
                        else if (rest == "-") vm.ExecuteBudgetDown();
                        else return "budget + or budget -";
                        break;
                    case "recall": vm.ExecuteRecall(); break;
                    case "picker":
                        if (rest.Length == 0) vm.ExecuteFoundNetwork();
                        else vm.ExecutePostHandler();
                        if (rest.Length > 0 && !vm.PickInPicker(rest)) return "Picker open, but no realm \"" + rest + "\" in it.\n" + vm.Describe();
                        break;
                    case "pick":
                        if (!vm.PickInPicker(rest)) return "Nothing called \"" + rest + "\" in an open picker.";
                        break;
                    case "post": vm.ExecuteSendHandler(); break;
                    case "cancel":
                        if (!int.TryParse(rest, out var op) || op < 0 || op >= vm.Operations.Count) return "No operation " + rest + ".";
                        vm.Operations[op].ExecuteCancel();
                        break;
                    default:
                        return "Unknown verb \"" + verb + "\".";
                }
                return vm.Describe();
            }
            catch (Exception ex)
            {
                return "test_intel failed: " + ex.Message;
            }
        }

        /// <summary>
        /// Sets war score from the first kingdom's point of view, so the peace table can be
        /// opened without fighting a war to a budget. Mirrors <c>set_smoothed_strength</c>:
        /// test only, never save over a real campaign.
        ///
        /// The record keeps one raw figure from the aggressor's side and
        /// <see cref="WarRecord.ScoreFor"/> negates it for the defender, so the delta is
        /// flipped when the first kingdom defends - without that, asking +90 for a
        /// defender set it to -90 (the first live pass hit exactly that).
        /// Usage: diplomacy.set_war_score Khuzait | Northern Empire | 50
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("set_war_score", "diplomacy")]
        public static string SetWarScore(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 3 || !float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var score))
                return "Usage: diplomacy.set_war_score <kingdom> | <kingdom> | <score for the first>";

            var a = FindKingdom(parts[0]);
            var b = FindKingdom(parts[1]);
            if (a == null || b == null) return "Kingdom not found.";

            var war = state.OngoingWarBetween(a, b);
            if (war == null) return a.Name + " and " + b.Name + " are not at war.";

            var delta = score - war.ScoreFor(a);
            war.AddWarScore(a == war.Aggressor ? delta : -delta);
            return a.Name + " war score set to " + war.ScoreFor(a).ToString("0.0")
                   + " (" + war.Aggressor.Name + " is the aggressor, raw "
                   + war.WarScore.ToString("0.0") + "). Test only - do not save.";
        }

        /// <summary>
        /// Opens the peace table for one side of a war through the real entry point
        /// (<see cref="UI.DiplomacyMenu.ShowPeace"/>), for verifying the screen without
        /// hunting a live budget: the demand face when that side is ahead, the offer face
        /// when it is behind.
        /// Usage: diplomacy.test_open_peace Khuzait | Northern Empire
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_open_peace", "diplomacy")]
        public static string TestOpenPeace(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.test_open_peace <us> | <them>";

            var us = FindKingdom(parts[0]);
            var them = FindKingdom(parts[1]);
            if (us == null || them == null) return "Kingdom not found.";

            try
            {
                UI.DiplomacyMenu.ShowPeace(state, us, them);
                return "Opened the peace path for " + us.Name + " vs " + them.Name + ".";
            }
            catch (Exception ex)
            {
                return "Could not open the peace table: " + ex.Message;
            }
        }

        /// <summary>
        /// Opens the incoming face of the peace table - an AI court's offer to <c>us</c> -
        /// with the callbacks disarmed, so the screen can be read and its buttons pressed
        /// without signing anything.
        ///
        /// The package is oriented the way the AI's own two paths orient it, decided by
        /// the war score: an offerer that is behind concedes (<c>TryBuyPeace</c> - we are
        /// the winner, and our captives come home), one that is ahead collects
        /// (<c>TryCollectPeace</c> - they are the winner, and we free theirs). The first
        /// version always made the offerer the winner, which for a losing offerer built a
        /// package no live path ever produces.
        /// Usage: diplomacy.test_open_incoming Northern Empire | Khuzait
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_open_incoming", "diplomacy")]
        public static string TestOpenIncoming(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.test_open_incoming <offerer> | <us>";

            var offerer = FindKingdom(parts[0]);
            var us = FindKingdom(parts[1]);
            if (offerer == null || us == null) return "Kingdom not found.";

            var war = state.OngoingWarBetween(offerer, us);
            if (war == null) return offerer.Name + " and " + us.Name + " are not at war.";

            try
            {
                var offererAhead = war.ScoreFor(offerer) > 0f;
                var winner = offererAhead ? offerer : us;
                var loser = offererAhead ? us : offerer;
                var terms = new PeaceTerms(winner, loser) { ReleasePrisoners = true };
                if (!PeaceTable.IsDemandable(state, war, terms, out _))
                    terms = new PeaceTerms(winner, loser);
                UI.Negotiation.PeaceTablePopup.ShowIncoming(state, war, us, offerer, terms,
                    () => { }, () => { });
                return "Opened " + offerer.Name + "'s offer as the "
                       + (offererAhead ? "winner collecting" : "loser conceding")
                       + " (test callbacks do nothing)."
                       + (terms.IsWhitePeace ? " White peace." : " Package: " + terms + ".");
            }
            catch (Exception ex)
            {
                return "Could not open the incoming table: " + ex.Message;
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
                if (!kingdom.IsRealm()) continue;
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

        // ----- Internal war (Phase 2.6) ---------------------------------------

        /// <summary>
        /// Every internal war, running and ended, then every kingdom against design 02 §6's
        /// three conditions - printed from <see cref="InternalWars.Assess"/>, the same answer
        /// the daily tick acts on.
        /// Usage: diplomacy.internal_wars
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("internal_wars", "diplomacy")]
        public static string InternalWarsReport(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var sb = new StringBuilder();
            sb.AppendLine("Map-faction index active: " + InternalWars.Any);

            if (state.InternalWars.Count == 0) sb.AppendLine("No internal war has been fought.");
            for (var i = 0; i < state.InternalWars.Count; i++)
            {
                var war = state.InternalWars[i];
                sb.AppendLine((war.IsOngoing ? "RUNNING  " : "ended    ") + war);
                sb.AppendLine("    started " + war.StartedOn + (war.IsOngoing ? "" : ", ended " + war.EndedOn)
                              + "; captive days: claimant " + war.ClaimantCaptiveDays + ", ruler "
                              + war.RulerCaptiveDays + "; crown side held "
                              + (war.CrownShareAtStart * 100f).ToString("0") + "% at the start");
                var names = new StringBuilder();
                for (var r = 0; r < war.Rebels.Count; r++)
                {
                    if (names.Length > 0) names.Append(", ");
                    names.Append(war.Rebels[r].Clan?.Name);
                }
                sb.AppendLine("    rebels: " + names);
            }

            sb.AppendLine();
            sb.AppendLine("-- every kingdom against the trigger (bloc >= "
                          + (IntrigueConstants.InternalWarBlocShare * 100f).ToString("0") + "%, legitimacy < "
                          + IntrigueConstants.InternalWarLegitimacy.ToString("0") + ", >= "
                          + IntrigueConstants.InternalWarDisloyalClans + " clans below "
                          + IntrigueConstants.LoyaltyDisaffected.ToString("0") + ") --");
            foreach (var kingdom in Kingdom.All)
            {
                if (!kingdom.IsRealm()) continue;
                var a = InternalWars.Assess(state, kingdom);
                sb.AppendLine(kingdom.Name + ": " + (a.Ready ? "READY" : "no") + " - " + a.Reason
                              + "  [bloc " + (a.BlocShare * 100f).ToString("0") + "%, legitimacy "
                              + a.Legitimacy.ToString("0.0") + ", disloyal " + a.DisloyalClans
                              + (a.Claim == null ? "" : ", claimant " + a.Claim.Claimant.Name
                                                        + ", would rise with " + a.Rebels.Count + " clan(s)")
                              + (a.PlayerWouldRebel ? ", the player would be asked" : "") + "]");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Starts an internal war now, skipping the three thresholds but not the claimant: the
        /// kingdom still needs a standing pretender in a pretender bloc, since a war has to be
        /// fought for someone. Test saves only.
        /// Usage: diplomacy.test_start_internal_war Battania
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_start_internal_war", "diplomacy")]
        public static string TestStartInternalWar(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            if (args == null || args.Count == 0) return "Usage: diplomacy.test_start_internal_war <kingdom>";

            var kingdom = FindKingdom(string.Join(" ", args));
            if (kingdom == null) return "No kingdom matching \"" + string.Join(" ", args) + "\".";

            var a = InternalWars.Assess(state, kingdom);
            if (a.Claim == null) return kingdom.Name + ": cannot start - " + a.Reason + ".";

            var war = InternalWars.Start(state, a, "forced by test_start_internal_war; thresholds "
                                                   + (a.Ready ? "were met" : "NOT met: " + a.Reason));
            return war == null
                ? kingdom.Name + ": the war did not start - see the log."
                : "Started: " + war + Environment.NewLine + MapFactionReport(state, kingdom);
        }

        /// <summary>
        /// Ends a running internal war with a chosen outcome, through the same code the daily
        /// tick uses. Test saves only.
        /// Usage: diplomacy.test_end_internal_war Battania | crown     (crown, rebels, stalemate)
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_end_internal_war", "diplomacy")]
        public static string TestEndInternalWar(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = string.Join(" ", args ?? new List<string>()).Split('|');
            if (parts.Length != 2) return "Usage: diplomacy.test_end_internal_war <kingdom> | crown|rebels|stalemate";

            var kingdom = FindKingdom(parts[0].Trim());
            if (kingdom == null) return "No kingdom matching \"" + parts[0].Trim() + "\".";

            var war = InternalWars.OngoingIn(state, kingdom);
            if (war == null) return kingdom.Name + " is not at war with itself.";

            InternalWarOutcome outcome;
            switch (parts[1].Trim().ToLowerInvariant())
            {
                case "crown": outcome = InternalWarOutcome.CrownWon; break;
                case "rebels": outcome = InternalWarOutcome.RebelsWon; break;
                case "stalemate": outcome = InternalWarOutcome.Stalemate; break;
                default: return "Outcome must be crown, rebels or stalemate.";
            }

            InternalWars.End(state, war, outcome, "forced by test_end_internal_war");
            return "Ended: " + war + Environment.NewLine + "Ruler now " + kingdom.Leader?.Name + " of "
                   + kingdom.RulingClan?.Name + ", legitimacy " + LegitimacyRegistry.Of(state, kingdom).ToString("0.0")
                   + Environment.NewLine + MapFactionReport(state, kingdom);
        }

        /// <summary>
        /// Every house of a kingdom at war with itself, with its price to change sides line by
        /// line, whether it can, and whether the other side's leader would pay - printed from
        /// <see cref="SideChange.QuoteFor"/> and <see cref="SideChange.AiWouldPay"/>, the same
        /// answers the Court tab and the weekly AI read.
        /// Usage: diplomacy.civil_war_prices Battania
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("civil_war_prices", "diplomacy")]
        public static string CivilWarPrices(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            if (args == null || args.Count == 0) return "Usage: diplomacy.civil_war_prices <kingdom>";

            var kingdom = FindKingdom(string.Join(" ", args));
            if (kingdom == null) return "No kingdom matching \"" + string.Join(" ", args) + "\".";
            var war = InternalWars.OngoingIn(state, kingdom);
            if (war == null) return kingdom.Name + " is not at war with itself.";

            var sb = new StringBuilder();
            sb.AppendLine(war.ToString());
            for (var side = 0; side < 2; side++)
            {
                var rising = side == 1;
                var leader = InternalWars.LeaderOf(war, rising);
                sb.AppendLine();
                sb.AppendLine("-- " + SideChange.SideName(war, rising) + ", led by " + leader?.Name
                              + " (purse " + (leader?.Gold ?? 0).ToString("N0") + ") --");
                foreach (var clan in Court.MembersOf(kingdom))
                {
                    if (war.IsRebel(clan) != rising) continue;
                    var q = SideChange.QuoteFor(state, war, clan);
                    if (q == null) continue;
                    sb.Append("  " + clan.Name + ": " + q.Price.ToString("N0") + " to " + SideChange.SideName(war, q.ToRising)
                              + " [strength " + q.Strength.ToString("0") + ", " + q.Towns + "T/" + q.Castles + "C, relation x"
                              + q.RelationFactor.ToString("0.00") + ", bond x" + q.BondFactor.ToString("0.00")
                              + ", momentum x" + q.MomentumFactor.ToString("0.00") + "]");
                    if (!q.Eligible) sb.AppendLine("  CANNOT: " + q.Reason);
                    else sb.AppendLine(SideChange.AiWouldPay(q, out var why) ? "  - would be paid" : "  - not paid: " + why);
                    for (var i = 0; i < q.Lines.Count; i++)
                        sb.AppendLine("      " + q.Lines[i].Key + ": " + q.Lines[i].Value.ToString("+#,0;-#,0;0"));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Moves a house to the other side of its kingdom's civil war now, through the same
        /// <see cref="SideChange.Execute"/> the Court tab and the AI use: eligibility still
        /// applies, the AI's budget rule does not. With <c>| unpaid</c> nobody pays - for a
        /// buyer whose purse the test cannot wait for. Test saves only.
        /// Usage: diplomacy.test_change_side fen Gruffendoc   or   ... | unpaid
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_change_side", "diplomacy")]
        public static string TestChangeSide(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = string.Join(" ", args ?? new List<string>()).Split('|');
            var clan = FindClan(parts[0].Trim());
            if (clan == null) return "Usage: diplomacy.test_change_side <clan> [| unpaid]";
            var paid = !(parts.Length > 1 && parts[1].Trim().Equals("unpaid", StringComparison.OrdinalIgnoreCase));

            var war = InternalWars.OngoingIn(state, clan.Kingdom);
            if (war == null) return clan.Name + "'s kingdom is not at war with itself.";

            var before = SideChange.QuoteFor(state, war, clan);
            if (!SideChange.Execute(state, war, clan, paid, out var failed)) return clan.Name + ": " + failed;
            return clan.Name + " went over to " + SideChange.SideName(war, before.ToRising)
                   + (paid ? " for " + before.Price.ToString("N0") : " unpaid") + ". Now: " + war
                   + Environment.NewLine + MapFactionReport(state, war.Kingdom);
        }

        /// <summary>
        /// Puts the player's house into a kingdom at war with itself, on a chosen side, so the
        /// Court tab can be seen from each of the three places a player can stand: a sworn house
        /// of the crown, a house of the rising, or the ruler. Joining the rising here is the start
        /// prompt's path, not a change of sides, so the house can still be bought afterwards.
        /// "ruler" hands the throne to the player's house with vanilla's
        /// <c>ChangeRulingClanAction</c>, which the succession watch will see as a change of
        /// ruler. Test saves only - this rewrites the player's allegiance.
        /// Usage: diplomacy.test_player_side Battania | crown     (crown, rising or ruler)
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_player_side", "diplomacy")]
        public static string TestPlayerSide(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = string.Join(" ", args ?? new List<string>()).Split('|');
            if (parts.Length != 2) return "Usage: diplomacy.test_player_side <kingdom> | crown|rising|ruler";
            var kingdom = FindKingdom(parts[0].Trim());
            if (kingdom == null) return "No kingdom matching \"" + parts[0].Trim() + "\".";
            var war = InternalWars.OngoingIn(state, kingdom);
            if (war == null) return kingdom.Name + " is not at war with itself.";

            var side = parts[1].Trim().ToLowerInvariant();
            if (side != "crown" && side != "rising" && side != "ruler") return "The side must be crown, rising or ruler.";

            var player = Clan.PlayerClan;
            if (player == null) return "No player clan.";
            if (player == kingdom.RulingClan || player == war.Banner) return "The player's house already leads a side.";

            if (player.Kingdom != kingdom)
            {
                if (player.Kingdom == null) ChangeKingdomAction.ApplyByJoinToKingdom(player, kingdom, default(CampaignTime), true);
                else ChangeKingdomAction.ApplyByJoinToKingdomByDefection(player, player.Kingdom, kingdom, default(CampaignTime), true);
            }
            if (player.Kingdom != kingdom) return "The player's house could not join " + kingdom.Name + ".";

            if (side == "rising")
            {
                if (!war.IsRebel(player)) InternalWars.JoinRising(state, war, player);
            }
            else
            {
                if (war.IsRebel(player)) return "The player's house is already with the rising; this lever does not move it back.";
                if (side == "ruler") ChangeRulingClanAction.Apply(kingdom, player);
            }

            return "The player's house is now " + (side == "ruler" ? "ruling " + kingdom.Name
                       : side == "rising" ? "with " + SideChange.SideName(war, true) : "with the crown of " + kingdom.Name)
                   + ". " + war + Environment.NewLine + MapFactionReport(state, kingdom);
        }

        /// <summary>
        /// Hands a kingdom's throne to the player's house, joining it first if needed, so a test
        /// can stand the player where a crown answers for its realm - the target of a demand for
        /// tribute, or the one making it. The same vanilla <c>ChangeRulingClanAction</c> that
        /// <c>test_player_side | ruler</c> uses, without needing a civil war; the succession
        /// watch sees it as a change of ruler, as it would any other. Refused while the player's
        /// house rules another kingdom, which would leave that realm without its ruling clan.
        /// Test saves only - this rewrites the player's allegiance and a kingdom's crown.
        /// Usage: diplomacy.test_player_rule Battania
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_player_rule", "diplomacy")]
        public static string TestPlayerRule(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            if (args == null || args.Count == 0) return "Usage: diplomacy.test_player_rule <kingdom>";

            var kingdom = FindKingdom(string.Join(" ", args));
            if (kingdom == null) return "No kingdom matching \"" + string.Join(" ", args) + "\".";
            if (!kingdom.IsRealm()) return kingdom.Name + " is not a realm.";
            if (InternalWars.OngoingIn(state, kingdom) != null)
                return kingdom.Name + " is at war with itself - use test_player_side, which places the player on a side.";

            var player = Clan.PlayerClan;
            if (player == null) return "No player clan.";
            if (kingdom.RulingClan == player) return "The player's house already rules " + kingdom.Name + ".";
            if (player.Kingdom != null && player.Kingdom != kingdom && player.Kingdom.RulingClan == player)
                return "The player's house rules " + player.Kingdom.Name + "; it cannot leave that realm without a crown.";

            if (player.Kingdom != kingdom)
            {
                if (player.Kingdom == null) ChangeKingdomAction.ApplyByJoinToKingdom(player, kingdom, default(CampaignTime), true);
                else ChangeKingdomAction.ApplyByJoinToKingdomByDefection(player, player.Kingdom, kingdom, default(CampaignTime), true);
            }
            if (player.Kingdom != kingdom) return "The player's house could not join " + kingdom.Name + ".";

            ChangeRulingClanAction.Apply(kingdom, player);
            return kingdom.RulingClan == player
                ? "The player's house now rules " + kingdom.Name + " (legitimacy "
                  + LegitimacyRegistry.Of(state, kingdom).ToString("0.0") + ")."
                : "ChangeRulingClanAction did not take: " + kingdom.Name + " is ruled by " + kingdom.RulingClan?.Name + ".";
        }

        /// <summary>
        /// One side of a civil war concedes now, through <see cref="InternalWars.Concede"/> - the
        /// Court tab's button. Test saves only.
        /// Usage: diplomacy.test_concede Battania | crown     (crown or rising: the side that gives up)
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_concede", "diplomacy")]
        public static string TestConcede(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = string.Join(" ", args ?? new List<string>()).Split('|');
            if (parts.Length != 2) return "Usage: diplomacy.test_concede <kingdom> | crown|rising";
            var kingdom = FindKingdom(parts[0].Trim());
            if (kingdom == null) return "No kingdom matching \"" + parts[0].Trim() + "\".";
            var war = InternalWars.OngoingIn(state, kingdom);
            if (war == null) return kingdom.Name + " is not at war with itself.";

            var which = parts[1].Trim().ToLowerInvariant();
            if (which != "crown" && which != "rising") return "The side must be crown or rising.";

            if (!InternalWars.Concede(state, war, which == "rising", out var failed)) return failed;
            return "Conceded: " + war + Environment.NewLine + "Ruler now " + kingdom.Leader?.Name + " of "
                   + kingdom.RulingClan?.Name + ", legitimacy " + LegitimacyRegistry.Of(state, kingdom).ToString("0.0");
        }

        /// <summary>
        /// What the engine's own code answers for every clan of a kingdom: the clan's map
        /// faction, its leader's, one of its parties' and one of its towns', and whether that
        /// party is at war with the ruler's party.
        ///
        /// The party and town columns are the real test of the MapFaction patches. Asking the
        /// clan only proves the postfix runs when *we* call the getter; `MobileParty.MapFaction`
        /// and `Town.MapFaction` call it from inside the game's assembly, where a getter this
        /// small could have been inlined out of the patch's reach.
        /// Usage: diplomacy.test_map_faction Battania
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_map_faction", "diplomacy")]
        public static string TestMapFaction(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            if (args == null || args.Count == 0) return "Usage: diplomacy.test_map_faction <kingdom>";

            var kingdom = FindKingdom(string.Join(" ", args));
            if (kingdom == null) return "No kingdom matching \"" + string.Join(" ", args) + "\".";

            return MapFactionReport(state, kingdom);
        }

        private static string MapFactionReport(ModState state, Kingdom kingdom)
        {
            var sb = new StringBuilder();
            var war = InternalWars.OngoingIn(state, kingdom);
            sb.AppendLine(kingdom.Name + (war == null ? " - no internal war" : " - internal war, rising " + war.Faction?.Name + " (" + war.Faction?.StringId + ")")
                          + "; index active " + InternalWars.Any);

            sb.AppendLine("  clan | clan.MapFaction | leader.MapFaction | party.MapFaction | town.MapFaction | party's faction vs the crown");

            for (var i = 0; i < kingdom.Clans.Count; i++)
            {
                var clan = kingdom.Clans[i];
                if (clan == null || clan.IsEliminated) continue;

                TaleWorlds.CampaignSystem.Party.MobileParty party = null;
                for (var p = 0; p < clan.WarPartyComponents.Count; p++)
                {
                    party = clan.WarPartyComponents[p]?.MobileParty;
                    if (party != null) break;
                }

                Settlement town = null;
                for (var s = 0; s < clan.Settlements.Count; s++)
                    if (clan.Settlements[s].IsFortification) { town = clan.Settlements[s]; break; }

                // Asked through the engine's own stance check, with the party's map faction as
                // the engine computes it - not with anything this mod holds.
                var hostile = party == null ? "-"
                    : FactionManager.IsAtWarAgainstFaction(party.MapFaction, kingdom) ? "WAR" : "peace";

                sb.AppendLine("  " + clan.Name
                              + (war != null && war.IsRebel(clan) ? " [rebel]" : "")
                              + (clan == kingdom.RulingClan ? " [crown]" : "")
                              + " | " + clan.MapFaction?.Name
                              + " | " + clan.Leader?.MapFaction?.Name
                              + " | " + (party == null ? "-" : party.MapFaction?.Name?.ToString())
                              + " | " + (town == null ? "-" : town.Name + "->" + town.MapFaction?.Name)
                              + " | " + hostile);
            }
            return sb.ToString();
        }

        // ----- A house divided (Phase 2.6b) ---------------------------------------

        /// <summary>
        /// For every court house (or one), what would happen if its head died today: the heirs
        /// as vanilla scores them, who would succeed, and whether the house would divide -
        /// printed from <see cref="ClanSuccession.Predict"/>, the same resolver the event uses.
        /// Usage: diplomacy.heirs   or   diplomacy.heirs fen Eingal
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("heirs", "diplomacy")]
        public static string Heirs(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            Clan only = null;
            if (args != null && args.Count > 0)
            {
                only = FindClan(string.Join(" ", args));
                if (only == null) return "No clan matching \"" + string.Join(" ", args) + "\".";
            }

            var sb = new StringBuilder();
            var dividing = 0;
            foreach (var kingdom in Kingdom.All)
            {
                if (!kingdom.IsRealm()) continue;
                foreach (var clan in Court.MembersOf(kingdom))
                {
                    if (only != null && clan != only) continue;
                    if (!clan.IsNoble || clan.Leader == null) continue;

                    var a = ClanSuccession.Predict(state, clan);
                    if (a.Divides) dividing++;
                    if (only == null && !a.Divides && a.RunnerUp == null) continue;

                    sb.AppendLine(kingdom.Name + " / " + clan.Name + (clan == kingdom.RulingClan ? " [ruling]" : "")
                                  + ": head " + clan.Leader.Name + " -> " + (a.Successor?.Name?.ToString() ?? "?")
                                  + " (" + a.SuccessorPoints + ")"
                                  + (a.RunnerUp == null ? "" : ", runner-up " + a.RunnerUp.Hero.Name + " (" + a.RunnerUp.Points
                                                               + ", relation " + a.Relation + ")")
                                  + " => " + (a.Divides ? "WOULD DIVIDE" : "holds") + " - " + a.Reason);
                    if (only != null)
                        for (var i = 0; i < a.Heirs.Count; i++)
                            sb.AppendLine("    " + a.Heirs[i].Hero.Name + "  " + a.Heirs[i].Points
                                          + "  age " + a.Heirs[i].Hero.Age.ToString("0")
                                          + "  " + KinTo(a.Heirs[i].Hero, clan.Leader));
                }
            }
            sb.AppendLine(dividing + " house(s) would divide at their head's death today. Thresholds: within "
                          + IntrigueConstants.ClanSuccessionContestMargin + " heir points, relation below "
                          + IntrigueConstants.ClanSuccessionDisputeRelation + ".");
            // Measured on the first live deaths: vanilla adjusts every hero's relation with the
            // new head during the succession itself (ChangeClanLeaderAction, before SetLeader),
            // and in both cases it warmed them - Sevin -38 to -24, Patyr -48 to -12.
            sb.AppendLine("Relations shown are today's. Vanilla moves them at the succession, so a"
                          + " prediction near the threshold may not hold.");
            return sb.ToString();
        }

        /// <summary>
        /// How a hero is related to a clan head, in the terms the succession blood claim reads
        /// (`SuccessionModel.HasBloodClaim`): child, parent, sibling - or other, which the blood
        /// claim does not admit once the hero has left the house.
        /// </summary>
        private static string KinTo(Hero hero, Hero head)
        {
            if (hero == null || head == null) return "";
            if (hero.Father == head || hero.Mother == head) return "child";
            if (head.Father == hero || head.Mother == hero) return "parent";
            foreach (var sibling in hero.Siblings) if (sibling == head) return "sibling";
            if (hero.Spouse == head) return "spouse";
            return "other kin";
        }

        /// <summary>
        /// Divides a house now, as if its head had died and the runner-up walked out - skipping
        /// the closeness and relation thresholds, not the mechanics. Test saves only. The founder
        /// is the best-scoring heir, or the one named after a bar.
        /// Usage: diplomacy.test_divide_clan fen Eingal   or   diplomacy.test_divide_clan Gundaroving | Simir
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_divide_clan", "diplomacy")]
        public static string TestDivideClan(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            if (args == null || args.Count == 0) return "Usage: diplomacy.test_divide_clan <clan> [| <hero>]";

            var parts = string.Join(" ", args).Split('|');
            var clan = FindClan(parts[0].Trim());
            if (clan == null) return "No clan matching \"" + parts[0].Trim() + "\".";
            var wanted = parts.Length > 1 ? parts[1].Trim() : null;

            // The head stays alive here, so the "successor" is the head itself and the runner-up
            // is the best-scoring heir: the split mechanics are exercised, the death is not.
            var a = ClanSuccession.Assess(state, clan, clan.Leader, clan.Leader);
            if (a.Heirs.Count == 0) return clan.Name + ": no eligible heir to leave.";
            a.RunnerUp = a.Heirs[0];
            if (a.RunnerUp.Hero == clan.Leader.Spouse && a.Heirs.Count > 1) a.RunnerUp = a.Heirs[1];
            if (wanted != null)
            {
                a.RunnerUp = null;
                for (var i = 0; i < a.Heirs.Count; i++)
                    if (string.Equals(a.Heirs[i].Hero.Name?.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
                        a.RunnerUp = a.Heirs[i];
                if (a.RunnerUp == null) return clan.Name + ": no eligible heir named " + wanted + ".";
            }

            var cadet = ClanSuccession.Divide(state, a);
            if (cadet == null) return clan.Name + ": the division did not happen - see the log.";

            var sb = new StringBuilder();
            sb.AppendLine(clan.Name + " divided: " + cadet.Name + " (" + cadet.StringId + "), tier " + cadet.Tier
                          + ", led by " + cadet.Leader?.Name + ", in " + cadet.Kingdom?.Name
                          + ", home " + cadet.HomeSettlement?.Name + ".");
            for (var i = 0; i < cadet.Heroes.Count; i++)
                sb.AppendLine("    " + cadet.Heroes[i].Name + "  party: "
                              + (cadet.Heroes[i].PartyBelongedTo?.Name?.ToString() ?? "none")
                              + "  settlement: " + (cadet.Heroes[i].CurrentSettlement?.Name?.ToString() ?? "none"));
            sb.AppendLine(clan.Name + " keeps " + clan.Heroes.Count + " hero(es); relation between the heads now "
                          + cadet.Leader?.GetRelation(clan.Leader) + ".");
            return sb.ToString();
        }

        // ----- Espionage (Phase 3) --------------------------------------------

        /// <summary>
        /// Spy networks, each with its week term by term - printed from
        /// <see cref="SpyNetworks.Explain"/>, the same sum the weekly upkeep applies - and the
        /// target's counter-intelligence.
        /// Usage: diplomacy.networks   or   diplomacy.networks <clan or kingdom>
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("networks", "diplomacy")]
        public static string Networks(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var filter = args == null || args.Count == 0 ? null : string.Join(" ", args).Trim();
            if (filter == string.Empty) filter = null;
            var clan = filter == null ? null : FindClan(filter);
            var kingdom = filter == null || clan != null ? null : FindKingdom(filter);
            if (filter != null && clan == null && kingdom == null) return "No clan or kingdom matching \"" + filter + "\".";

            var sb = new StringBuilder();
            var shown = 0;
            for (var i = 0; i < state.SpyNetworks.Count; i++)
            {
                var n = state.SpyNetworks[i];
                if (clan != null && n.Owner != clan) continue;
                if (kingdom != null && n.Target != kingdom && n.Owner?.Kingdom != kingdom) continue;
                shown++;

                var t = SpyNetworks.Explain(state, n);
                sb.AppendLine(n.Owner?.Name + " in " + n.Target?.Name + ": strength " + n.Strength.ToString("0.0")
                              + " of " + t.Ceiling.ToString("0") + ", budget " + n.WeeklyBudget + "/week"
                              + ", last week " + n.LastWeekChange.ToString("+0.00;-0.00;0.00")
                              + " for " + n.LastWeekSpent + " spent");
                sb.AppendLine("    handler: " + (n.Handler == null ? "none" : n.Handler.Name
                              + " (roguery " + t.Roguery.ToString("0") + ", charm " + t.Charm.ToString("0")
                              + ") at " + (n.Handler.CurrentSettlement?.Name?.ToString() ?? "no settlement")));
                if (t.Idle != null) sb.AppendLine("    idle: " + t.Idle + " - nothing is spent, nothing grows");
                sb.AppendLine("    next week: gold " + t.Spend + " -> " + t.FromGold.ToString("+0.00;-0.00;0.00")
                              + (t.AtWar ? " x " + EspionageConstants.NetworkWartimeGrowth.ToString("0.0") + " at war = " + t.Investment.ToString("+0.00;-0.00;0.00") : "")
                              + ", counter-intelligence " + t.CounterIntelligence.ToString("0.0") + " -> "
                              + (-t.FromCounterIntelligence).ToString("+0.00;-0.00;0.00")
                              + ", attrition " + (-t.Attrition).ToString("+0.00;-0.00;0.00")
                              + " = " + t.Weekly.ToString("+0.00;-0.00;0.00")
                              + ", and " + (-t.WeekOfDecay).ToString("+0.00;-0.00;0.00") + " of daily decay = "
                              + t.NetOverAWeek.ToString("+0.00;-0.00;0.00") + " a week");
            }
            if (shown == 0) sb.AppendLine("No spy networks" + (filter == null ? "." : " for \"" + filter + "\"."));

            if (kingdom != null)
                sb.AppendLine(kingdom.Name + "'s counter-intelligence: " + CounterIntelligence.Explain(state, kingdom));
            return sb.ToString();
        }

        /// <summary>
        /// Puts a hero of a clan in charge of that clan's network in a realm, founding it if needed,
        /// through <see cref="SpyNetworks.Assign"/> - every eligibility rule applies. An optional
        /// weekly budget is set at the same time. Test saves only until the espionage UI (3.7).
        /// Usage: diplomacy.test_assign_handler <hero> | <kingdom> [| weekly denars]
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_assign_handler", "diplomacy")]
        public static string TestAssignHandler(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.test_assign_handler <hero> | <kingdom> [| weekly denars]";
            var hero = FindHero(parts[0]);
            if (hero == null) return "No living hero matching \"" + parts[0] + "\".";
            var target = FindKingdom(parts[1]);
            if (target == null) return "No kingdom matching \"" + parts[1] + "\".";

            var network = SpyNetworks.Assign(state, hero, hero.Clan, target, out var reason);
            if (network == null) return "Refused: " + reason;
            if (parts.Count >= 3 && int.TryParse(parts[2], out var budget)) SpyNetworks.SetBudget(state, hero.Clan, target, budget);
            return "Assigned. " + network + Environment.NewLine + Networks(new List<string> { hero.Clan.Name.ToString() });
        }

        /// <summary>
        /// Sets a clan's weekly budget for its network in a realm.
        /// Usage: diplomacy.test_network_budget <clan> | <kingdom> | <weekly denars>
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_network_budget", "diplomacy")]
        public static string TestNetworkBudget(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 3 || !int.TryParse(parts[2], out var budget))
                return "Usage: diplomacy.test_network_budget <clan> | <kingdom> | <weekly denars>";
            var clan = FindClan(parts[0]);
            if (clan == null) return "No clan matching \"" + parts[0] + "\".";
            var target = FindKingdom(parts[1]);
            if (target == null) return "No kingdom matching \"" + parts[1] + "\".";

            var network = SpyNetworks.SetBudget(state, clan, target, budget);
            return "Budget set. " + network;
        }

        /// <summary>
        /// Sets a network's strength outright, within its handler's ceiling - for testing missions
        /// that need a network the weekly growth would take months to build. The growth itself was
        /// verified on the real clock (design 03 §10); this lever skips it. Test saves only.
        /// Usage: diplomacy.test_set_network <clan> | <kingdom> | <strength>
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_set_network", "diplomacy")]
        public static string TestSetNetwork(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 3 || !float.TryParse(parts[2], out var strength))
                return "Usage: diplomacy.test_set_network <clan> | <kingdom> | <strength>";
            var clan = FindClan(parts[0]);
            if (clan == null) return "No clan matching \"" + parts[0] + "\".";
            var target = FindKingdom(parts[1]);
            if (target == null) return "No kingdom matching \"" + parts[1] + "\".";

            var network = SpyNetworks.Get(state, clan, target);
            if (network == null) return clan.Name + " has no network in " + target.Name + ".";
            var ceiling = network.Handler == null ? EspionageConstants.NetworkMaxStrength : SpyNetworks.CeilingOf(network.Handler);
            network.Change(strength - network.Strength, ceiling);
            return "Set. " + network;
        }

        /// <summary>
        /// Runs one week of espionage upkeep now - the weekly half only, so it can be read against
        /// the prediction diplomacy.networks printed: counter-intelligence budgets paid, then the
        /// networks, through the same list the campaign runs. Pair it with tick_days 7 for a whole week.
        /// Usage: diplomacy.test_network_week
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_network_week", "diplomacy")]
        public static string TestNetworkWeek(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            EspionageUpkeep.Weekly(state);
            return "Ran the weekly espionage upkeep once: the AI's choices, counter-intelligence budgets, then networks (no daily decay)."
                   + Environment.NewLine + Networks(new List<string>())
                   + CounterIntelligenceReport(new List<string>());
        }

        /// <summary>
        /// What each AI realm's espionage would do this week, and why - printed from
        /// <see cref="AiEspionage.Plan"/>, the object the weekly run executes. A dry run: nothing is
        /// founded, paid or launched. With a kingdom, that realm only.
        /// Usage: diplomacy.ai_espionage   or   diplomacy.ai_espionage <kingdom>
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("ai_espionage", "diplomacy")]
        public static string AiEspionageReport(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var filter = args == null || args.Count == 0 ? null : string.Join(" ", args).Trim();
            Kingdom only = null;
            if (!string.IsNullOrEmpty(filter))
            {
                only = FindKingdom(filter);
                if (only == null) return "No kingdom matching \"" + filter + "\".";
            }

            var sb = new StringBuilder();
            foreach (var kingdom in Kingdom.All)
            {
                if (!kingdom.IsRealm() || (only != null && kingdom != only)) continue;
                sb.Append(AiEspionage.Describe(AiEspionage.Plan(state, kingdom)));
            }
            return sb.Length == 0 ? "No realm." : sb.ToString();
        }

        /// <summary>
        /// Every realm's counter-intelligence, term by term - printed from
        /// <see cref="CounterIntelligence.Explain"/>, the value network growth, mission odds and
        /// exposure all read. With a kingdom, that realm only.
        /// Usage: diplomacy.counter_intelligence   or   diplomacy.counter_intelligence <kingdom>
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("counter_intelligence", "diplomacy")]
        public static string CounterIntelligenceReport(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var filter = args == null || args.Count == 0 ? null : string.Join(" ", args).Trim();
            Kingdom only = null;
            if (!string.IsNullOrEmpty(filter))
            {
                only = FindKingdom(filter);
                if (only == null) return "No kingdom matching \"" + filter + "\".";
            }

            var sb = new StringBuilder();
            foreach (var kingdom in Kingdom.All)
            {
                if (!kingdom.IsRealm() || (only != null && kingdom != only)) continue;
                sb.AppendLine(kingdom.Name.ToString().PadRight(18) + CounterIntelligence.Explain(state, kingdom)
                              + ", paid by " + (kingdom.Leader?.Name?.ToString() ?? "nobody")
                              + " (purse " + (kingdom.Leader?.Gold ?? 0) + ")");
            }
            return sb.Length == 0 ? "No realm." : sb.ToString();
        }

        /// <summary>
        /// Sets a realm's weekly counter-intelligence budget, through
        /// <see cref="CounterIntelligence.SetBudget"/> - the ruler pays it at the next weekly
        /// upkeep. Test saves only until the espionage UI (3.7).
        /// Usage: diplomacy.test_counter_budget <kingdom> | <weekly denars>
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_counter_budget", "diplomacy")]
        public static string TestCounterBudget(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2 || !int.TryParse(parts[1], out var weekly))
                return "Usage: diplomacy.test_counter_budget <kingdom> | <weekly denars>";
            var kingdom = FindKingdom(parts[0]);
            if (kingdom == null) return "No kingdom matching \"" + parts[0] + "\".";

            var budget = CounterIntelligence.SetBudget(state, kingdom, weekly, out var reason);
            return budget == null
                ? "Refused: " + reason
                : "Ordered. " + budget + " - paid at the next weekly upkeep (test_network_week runs one now)."
                  + Environment.NewLine + CounterIntelligenceReport(new List<string> { kingdom.Name.ToString() });
        }

        /// <summary>
        /// Every mission's odds for one network, term by term - printed from
        /// <see cref="Missions.OddsOf"/>, the one resolver the roll, the mission board and the AI read.
        /// Usage: diplomacy.mission_odds <clan> | <kingdom>
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("mission_odds", "diplomacy")]
        public static string MissionOdds(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.mission_odds <clan> | <kingdom>";
            var clan = FindClan(parts[0]);
            if (clan == null) return "No clan matching \"" + parts[0] + "\".";
            var target = FindKingdom(parts[1]);
            if (target == null) return "No kingdom matching \"" + parts[1] + "\".";

            var network = SpyNetworks.Get(state, clan, target);
            if (network == null) return clan.Name + " has no network in " + target.Name + ".";

            var sb = new StringBuilder();
            sb.AppendLine(clan.Name + " in " + target.Name + ": network " + network.Strength.ToString("0.0")
                          + ", handler " + (network.Handler == null ? "none" : network.Handler.Name.ToString()));
            foreach (var spec in Missions.AllSpecs)
            {
                var o = Missions.OddsOf(state, network, spec.Type);
                sb.AppendLine("  " + spec.Type.ToString().PadRight(17) + " needs " + spec.Required.ToString("0").PadLeft(2)
                              + ", " + spec.Gold.ToString().PadLeft(5) + " gold, " + spec.Days.ToString().PadLeft(2) + " days | "
                              + "0.15 + network " + o.FromNetwork.ToString("0.000") + " + handler " + o.FromHandler.ToString("0.000")
                              + " - counter-intel " + o.FromCounterIntelligence.ToString("0.000")
                              + " - difficulty " + o.Difficulty.ToString("0.000") + " = " + o.RawSuccess.ToString("0.000")
                              + " -> success " + Missions.Pct(o.Success) + ", exposed if it fails " + Missions.Pct(o.ExposureOnFailure)
                              + ", exposed overall " + Missions.Pct(o.Exposure)
                              + (network.Strength < spec.Required ? "   (network too weak)" : "")
                              + (spec.NotYet != null ? "   (not yet: " + spec.NotYet + ")" : ""));
            }
            sb.AppendLine("  (counter-intelligence " + CounterIntelligence.Explain(state, target) + ")");
            return sb.ToString();
        }

        /// <summary>
        /// Spy missions, pending and recently resolved.
        /// Usage: diplomacy.missions   or   diplomacy.missions <clan or kingdom>
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("missions", "diplomacy")]
        public static string MissionList(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var filter = args == null || args.Count == 0 ? null : string.Join(" ", args).Trim();
            if (filter == string.Empty) filter = null;
            var clan = filter == null ? null : FindClan(filter);
            var kingdom = filter == null || clan != null ? null : FindKingdom(filter);

            var sb = new StringBuilder();
            for (var i = 0; i < state.SpyMissions.Count; i++)
            {
                var m = state.SpyMissions[i];
                if (clan != null && m.Owner != clan) continue;
                if (kingdom != null && m.Target != kingdom && m.Owner?.Kingdom != kingdom) continue;
                sb.AppendLine(m + (m.IsPending
                    ? " - resolves in " + (m.ResolvesOn - CampaignTime.Now).ToDays.ToString("0.0") + " days"
                    : " - resolved " + (CampaignTime.Now - m.ResolvedOn).ToDays.ToString("0") + " days ago")
                    + ", handler " + m.Handler?.Name + ", paid " + m.GoldPaid);
            }
            return sb.Length == 0 ? "No spy missions." : sb.ToString();
        }

        /// <summary>
        /// Every successful bribe still on the record, and whether it binds anybody today - read
        /// through <see cref="Bribes.On"/>, the resolver loyalty and the internal war read.
        /// Usage: diplomacy.bribes
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("bribes", "diplomacy")]
        public static string BribeList(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var sb = new StringBuilder();
            for (var i = 0; i < state.SpyMissions.Count; i++)
            {
                var m = state.SpyMissions[i];
                if (m.Type != SpyMissionType.BribeLord || m.Outcome != MissionOutcome.Success) continue;

                var clan = m.TargetHero?.Clan;
                var live = clan != null && Bribes.On(state, clan) == m;
                sb.AppendLine(m.TargetHero?.Name + " of " + clan?.Name + ", bought by " + m.Owner?.Name
                              + " in " + m.Target?.Name + " " + (CampaignTime.Now - m.ResolvedOn).ToDays.ToString("0") + " days ago: "
                              + (live
                                  ? "binds, " + Bribes.DaysLeft(state, clan).ToString("0") + " days left; loyalty "
                                    + LoyaltyModel.Explain(state, clan)
                                  : "binds nobody (expired, superseded, or the lord no longer heads a house of that realm)"));
            }
            return sb.Length == 0 ? "No bribes on the record." : sb.ToString();
        }

        /// <summary>
        /// Launches an operation through <see cref="Missions.Launch"/> - every rule applies, and it
        /// is paid for. The optional target is a town or castle, or a lord, as the mission needs.
        /// Usage: diplomacy.test_launch_mission <clan> | <kingdom> | <type> [| settlement or hero]
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_launch_mission", "diplomacy")]
        public static string TestLaunchMission(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 3) return "Usage: diplomacy.test_launch_mission <clan> | <kingdom> | <type> [| settlement or hero]";
            var clan = FindClan(parts[0]);
            if (clan == null) return "No clan matching \"" + parts[0] + "\".";
            var target = FindKingdom(parts[1]);
            if (target == null) return "No kingdom matching \"" + parts[1] + "\".";
            if (!Enum.TryParse(parts[2], true, out SpyMissionType type) || type == SpyMissionType.None)
                return "Unknown mission \"" + parts[2] + "\". Types: " + string.Join(", ", Enum.GetNames(typeof(SpyMissionType)));

            var spec = Missions.SpecOf(type);
            Settlement settlement = null;
            Hero hero = null;
            if (parts.Count >= 4)
            {
                if (spec.NeedsSettlement) settlement = FindSettlement(parts[3]);
                if (spec.NeedsHero) hero = FindHero(parts[3]);
            }

            var mission = Missions.Launch(state, clan, target, type, hero, settlement, out var reason);
            return mission == null ? "Refused: " + reason : "Launched: " + mission + Environment.NewLine + MissionList(new List<string> { clan.Name.ToString() });
        }

        /// <summary>
        /// Resolves a clan's pending operation in a realm now, through <see cref="Missions.Resolve"/>.
        /// Without an outcome it rolls, exactly as the daily tick does; with one, the roll is
        /// skipped and everything after it runs unchanged. Test saves only.
        /// Usage: diplomacy.test_resolve_mission <clan> | <kingdom> [| success|failure|exposed]
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_resolve_mission", "diplomacy")]
        public static string TestResolveMission(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;

            var parts = SplitOnPipe(args);
            if (parts.Count < 2) return "Usage: diplomacy.test_resolve_mission <clan> | <kingdom> [| success|failure|exposed]";
            var clan = FindClan(parts[0]);
            if (clan == null) return "No clan matching \"" + parts[0] + "\".";
            var target = FindKingdom(parts[1]);
            if (target == null) return "No kingdom matching \"" + parts[1] + "\".";

            MissionOutcome? forced = null;
            if (parts.Count >= 3)
            {
                if (!Enum.TryParse(parts[2], true, out MissionOutcome outcome) || outcome == MissionOutcome.Pending)
                    return "The outcome must be success, failure or exposed.";
                forced = outcome;
            }

            var mission = Missions.PendingOn(state, clan, target);
            if (mission == null) return clan.Name + " is running nothing in " + target.Name + ".";
            var result = Missions.Resolve(state, mission, forced);
            return "Resolved " + result + ": " + mission + Environment.NewLine + Networks(new List<string> { clan.Name.ToString() });
        }

        /// <summary>
        /// Hires a wanderer into the player's clan and puts them in the main party, through
        /// vanilla's AddCompanionAction and AddHeroToPartyAction - so a test save with no companion
        /// can give the player a handler who starts where a real one would, in the party.
        /// Test saves only.
        /// Usage: diplomacy.test_hire_companion Synira the Wanderer
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("test_hire_companion", "diplomacy")]
        public static string TestHireCompanion(List<string> args)
        {
            var state = CoreBehavior.State;
            if (state == null) return NoCampaign;
            if (args == null || args.Count == 0) return "Usage: diplomacy.test_hire_companion <wanderer>";

            var hero = FindHero(string.Join(" ", args));
            if (hero == null) return "No living hero matching \"" + string.Join(" ", args) + "\".";
            if (!hero.IsWanderer || hero.Clan != null) return hero.Name + " is not a free wanderer.";
            if (Clan.PlayerClan == null || TaleWorlds.CampaignSystem.Party.MobileParty.MainParty == null) return "No player clan or party.";

            AddCompanionAction.Apply(Clan.PlayerClan, hero);
            AddHeroToPartyAction.Apply(hero, TaleWorlds.CampaignSystem.Party.MobileParty.MainParty, true);
            return hero.Name + " joins " + Clan.PlayerClan.Name + " (roguery " + hero.GetSkillValue(DefaultSkills.Roguery)
                   + ", charm " + hero.GetSkillValue(DefaultSkills.Charm) + "), in "
                   + (hero.PartyBelongedTo?.Name?.ToString() ?? "no party") + ".";
        }

        /// <summary>
        /// A living hero by string id, then by name - the player's own clan first, since a
        /// handler is usually one of ours and names repeat across Calradia ("Sinor" is three
        /// heroes) - then by prefix.
        /// </summary>
        private static Hero FindHero(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            name = name.Trim();
            foreach (var hero in Hero.AllAliveHeroes)
                if (string.Equals(hero.StringId, name, StringComparison.OrdinalIgnoreCase)) return hero;
            if (Clan.PlayerClan != null)
                foreach (var hero in Clan.PlayerClan.Heroes)
                    if (hero.IsAlive && string.Equals(hero.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase)) return hero;
            foreach (var hero in Hero.AllAliveHeroes)
                if (string.Equals(hero.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase)) return hero;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                var heroName = hero.Name?.ToString();
                if (heroName != null && heroName.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return hero;
            }
            return null;
        }

        private static Clan FindClan(string name)
        {
            foreach (var clan in Clan.All)
                if (string.Equals(clan.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase)) return clan;
            foreach (var clan in Clan.All)
            {
                var clanName = clan.Name?.ToString();
                if (clanName != null && clanName.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return clan;
            }
            return null;
        }

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
