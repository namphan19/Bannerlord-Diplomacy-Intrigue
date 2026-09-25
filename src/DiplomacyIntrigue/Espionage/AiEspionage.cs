using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace DiplomacyIntrigue.Espionage
{
    /// <summary>How much of a rival one realm is to another, and why.</summary>
    public sealed class EspionageTarget
    {
        public Kingdom Kingdom;
        public float Score;
        public string Why;
    }

    /// <summary>One operation the AI considered: its mark, what it is worth, and why it was or was not chosen.</summary>
    public sealed class EspionageMissionChoice
    {
        public SpyMissionType Type;
        public Hero Hero;
        public Settlement Settlement;
        public float Value;
        public MissionOdds Odds;
        public string Why;

        /// <summary>Null when it could be launched; otherwise the gate that stopped it.</summary>
        public string Refused;
    }

    /// <summary>
    /// What an AI realm means to do with espionage this week, and every reason behind it. The
    /// weekly run executes it and <c>diplomacy.ai_espionage</c> prints it: one object, so the
    /// diagnostic cannot describe a decision the AI did not make (the shape of
    /// <c>InternalWars.Assess</c>).
    /// </summary>
    public sealed class EspionagePlan
    {
        public Kingdom Realm;
        public Clan Owner;
        public Hero Ruler;

        /// <summary>Why the realm does nothing at all this week, if it does nothing.</summary>
        public string Skip;

        public int Purse;
        public int Spendable;

        public float Threat;
        public string ThreatWhy;
        public int CounterBudget;

        public readonly List<EspionageTarget> Targets = new List<EspionageTarget>();
        public Kingdom Target;
        public string TargetWhy;

        public SpyNetwork Network;
        public Hero Handler;
        public string HandlerWhy;
        public int NetworkBudget;

        public readonly List<EspionageMissionChoice> Candidates = new List<EspionageMissionChoice>();
        public EspionageMissionChoice Mission;
        public string MissionWhy;
    }

    /// <summary>
    /// The AI's espionage (design 03 §8 step 3.6), under the lead's calls of 2026-09-25 (design 03
    /// §9, decisions 11-13): only a ruling house runs a network, one at most, aimed at a clear rival;
    /// it spends only what its purse can spare; it acts only when being caught is unlikely; and it
    /// assassinates only in war, only a commander in the field, never a ruler and never the player's
    /// own people.
    ///
    /// **The same rules as the player.** Everything the AI does goes through the calls the player's
    /// levers use - <see cref="SpyNetworks.Assign"/>, <see cref="SpyNetworks.SetBudget"/>,
    /// <see cref="CounterIntelligence.SetBudget"/>, <see cref="Missions.Launch"/> - so every gate,
    /// price and odds is the player's. What is the AI's own is only the choosing.
    ///
    /// **ScoutArmies and ReadCourt are never chosen.** The AI already reads the numbers those two
    /// sell - exhaustion, loyalty, blocs, armies - and paying for a reveal it does not need would be
    /// theatre. That is an asymmetry the project had before espionage existed (the AI's diplomacy
    /// reads exact figures the player sees as bands), recorded here rather than hidden.
    ///
    /// The player's own realm is never planned for: its espionage is the player's.
    /// </summary>
    public static class AiEspionage
    {
        // ----- Planning --------------------------------------------------------

        public static EspionagePlan Plan(ModState state, Kingdom realm)
        {
            var p = new EspionagePlan { Realm = realm };
            if (state == null || realm == null || !realm.IsRealm()) { p.Skip = "not a realm"; return p; }

            p.Ruler = realm.Leader;
            p.Owner = realm.RulingClan;
            if (p.Ruler == null || p.Owner == null) { p.Skip = "nobody on the throne"; return p; }
            if (p.Ruler == Hero.MainHero) { p.Skip = "the player rules it"; return p; }

            p.Purse = p.Ruler.Gold;
            p.Spendable = Math.Max(0, p.Purse - EspionageConstants.AiGoldReserve);

            PlanCounterIntelligence(state, p);
            PlanTarget(state, p);
            PlanNetwork(state, p);
            PlanMission(state, p);
            return p;
        }

        /// <summary>
        /// Defence scales with the threats a court can actually see: the wars it is fighting, and the
        /// foreign agents it has caught (the live EspionageExposed claims it holds). Networks nobody
        /// has caught are, by definition, invisible, and the AI does not read them.
        /// </summary>
        private static void PlanCounterIntelligence(ModState state, EspionagePlan p)
        {
            var wars = 0;
            var caught = 0;
            foreach (var k in Kingdom.All)
            {
                if (k == p.Realm || !k.IsRealm()) continue;
                if (p.Realm.IsAtWarWith(k)) wars++;
                foreach (var claim in ClaimRegistry.LiveClaims(state, p.Realm, k))
                    if (claim.Type == CasusBelliType.EspionageExposed) caught++;
            }

            p.Threat = wars * EspionageConstants.AiCounterThreatPerWar + caught * EspionageConstants.AiCounterThreatPerExposure;
            p.ThreatWhy = wars + " war(s), " + caught + " intrusion(s) caught";

            var wanted = (int)(p.Threat * EspionageConstants.AiCounterBudgetPerThreat);
            var affordable = (int)(p.Spendable * EspionageConstants.AiCounterBudgetShare);
            p.CounterBudget = RoundDown(Math.Min(Math.Min(wanted, affordable), EspionageConstants.AiCounterBudgetCap));
        }

        private static void PlanTarget(ModState state, EspionagePlan p)
        {
            var us = p.Realm;
            foreach (var them in Kingdom.All)
            {
                if (them == us || !them.IsRealm()) continue;
                if (IsBoundTo(state, us, them)) continue;

                var score = 0f;
                var why = new StringBuilder();
                if (us.IsAtWarWith(them)) { score += EspionageConstants.AiTargetScoreWar; why.Append("at war; "); }
                if (ClaimRegistry.HasTerritorialClaim(state, us, them)) { score += EspionageConstants.AiTargetScoreClaim; why.Append("we claim their land; "); }
                if (ClaimRegistry.HasTerritorialClaim(state, them, us)) { score += EspionageConstants.AiTargetScoreClaimedBy; why.Append("they claim ours; "); }
                if (Power.Balance(them, us) > 0f && AiDiplomacy.Proximity(us, them) >= EspionageConstants.AiNeighbourProximity)
                {
                    score += EspionageConstants.AiTargetScoreStrongerNeighbour;
                    why.Append("a stronger neighbour; ");
                }
                if (score <= 0f) continue;
                p.Targets.Add(new EspionageTarget { Kingdom = them, Score = score, Why = why.ToString().TrimEnd(' ', ';') });
            }
            p.Targets.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Keep the network where it is while that realm is still a rival: agents are years in the
            // making, and moving them to whoever scores a point higher this week would throw that away.
            var current = CurrentNetwork(state, p.Owner);
            if (current != null)
                for (var i = 0; i < p.Targets.Count; i++)
                    if (p.Targets[i].Kingdom == current.Target && p.Targets[i].Score >= EspionageConstants.AiTargetMinScore)
                    {
                        p.Target = current.Target;
                        p.TargetWhy = "kept: " + p.Targets[i].Why;
                        return;
                    }

            if (p.Targets.Count > 0 && p.Targets[0].Score >= EspionageConstants.AiTargetMinScore)
            {
                p.Target = p.Targets[0].Kingdom;
                p.TargetWhy = p.Targets[0].Why;
            }
            else p.TargetWhy = "no clear rival";
        }

        private static void PlanNetwork(ModState state, EspionagePlan p)
        {
            if (p.Target == null) { p.HandlerWhy = "no target"; return; }

            p.Network = SpyNetworks.Get(state, p.Owner, p.Target);
            if (p.Network?.Handler != null)
            {
                p.Handler = p.Network.Handler;
                p.HandlerWhy = "in post";
            }
            else
            {
                // The best ceiling among the house's free heroes. The handler's roguery and charm cap
                // what the network can ever become, so this is the choice that matters most.
                var best = -1f;
                foreach (var hero in p.Owner.Heroes)
                {
                    if (!SpyNetworks.CanHandle(state, hero, p.Owner, p.Target, out _)) continue;
                    var ceiling = SpyNetworks.CeilingOf(hero);
                    if (ceiling <= best) continue;
                    best = ceiling;
                    p.Handler = hero;
                }
                p.HandlerWhy = p.Handler == null
                    ? "nobody in the house is free to go (every grown member leads, governs or heads it)"
                    : "chosen, ceiling " + best.ToString("0");
            }

            if (p.Handler != null)
                p.NetworkBudget = RoundDown(Math.Min((int)(p.Spendable * EspionageConstants.AiNetworkBudgetShare),
                                                     EspionageConstants.AiNetworkBudgetCap));
        }

        private static void PlanMission(ModState state, EspionagePlan p)
        {
            var network = p.Network;
            if (network == null || network.Handler == null) { p.MissionWhy = "no network running"; return; }
            if (Missions.PendingOn(state, p.Owner, p.Target) != null) { p.MissionWhy = "an operation is under way"; return; }
            if (InCooldown(state, p.Owner, p.Target, out var daysLeft))
            {
                p.MissionWhy = "resting after the last operation (" + daysLeft.ToString("0") + " days)";
                return;
            }

            var us = p.Realm;
            var them = p.Target;
            var atWar = us.IsAtWarWith(them);
            var claim = ClaimRegistry.HasTerritorialClaim(state, us, them);

            // Bribes and forgeries aim at a civil war, and only a shaky crown has one coming.
            var shaky = LegitimacyRegistry.Of(state, them) < EspionageConstants.AiSubvertLegitimacy
                        || SuccessionModel.PretendersTo(state, them).Count > 0;
            if (shaky)
            {
                Hero bribe = null, forge = null;
                var bribeLoyalty = float.MaxValue;
                var forgeLoyalty = float.MaxValue;
                foreach (var clan in Court.MembersOf(them))
                {
                    if (clan == them.RulingClan || clan.Leader == null) continue;
                    var loyalty = LoyaltyModel.Of(state, clan);
                    if (loyalty < EspionageConstants.AiBribeMaxLoyalty && !Bribes.IsBought(state, clan) && loyalty < bribeLoyalty)
                    {
                        bribeLoyalty = loyalty;
                        bribe = clan.Leader;
                    }
                    // Not the player's house: forged letters work by deceiving a lord, and the player
                    // cannot be deceived about letters they never received.
                    if (clan != Clan.PlayerClan && loyalty >= EspionageConstants.AiForgeMinLoyalty
                        && loyalty < EspionageConstants.AiForgeMaxLoyalty && loyalty < forgeLoyalty)
                    {
                        forgeLoyalty = loyalty;
                        forge = clan.Leader;
                    }
                }
                if (bribe != null)
                    Consider(state, p, SpyMissionType.BribeLord, bribe, null,
                             2f + (EspionageConstants.AiBribeMaxLoyalty - bribeLoyalty) / 20f,
                             bribe.Clan.Name + " at loyalty " + bribeLoyalty.ToString("0.0"));
                if (forge != null)
                    Consider(state, p, SpyMissionType.ForgeLetters, forge, null, 1.5f,
                             forge.Clan.Name + " at loyalty " + forgeLoyalty.ToString("0.0"));
            }

            if (atWar || claim)
            {
                var town = DissentTarget(us, them);
                if (town != null)
                    Consider(state, p, SpyMissionType.SpreadDissent, null, town, 1.5f,
                             town.Name + ", the nearest of theirs, loyalty " + town.Town.Loyalty.ToString("0"));

                var ruler = them.Leader;
                var spec = Missions.SpecOf(SpyMissionType.StealTreasury);
                var take = ruler == null ? 0 : Math.Min((int)(ruler.Gold * EspionageConstants.StealTreasuryShare), EspionageConstants.StealTreasuryCap);
                if (take >= spec.Gold * EspionageConstants.AiStealMinReturn)
                    Consider(state, p, SpyMissionType.StealTreasury, null, null, take / 10000f,
                             "a take of " + take + " for " + spec.Gold);
            }

            if (atWar)
            {
                var besieged = BesiegedByUs(us, them);
                if (besieged != null)
                    Consider(state, p, SpyMissionType.SabotageGarrison, null, besieged, 4f,
                             besieged.Name + " is under our siege");

                var commander = FieldCommander(them);
                var price = Missions.SpecOf(SpyMissionType.Assassinate).Gold;
                if (commander != null && p.Spendable >= price * EspionageConstants.AiAssassinateGoldMultiple)
                    Consider(state, p, SpyMissionType.Assassinate, commander, null, 3f,
                             commander.Name + " leads their largest army");
            }

            for (var i = 0; i < p.Candidates.Count; i++)
            {
                var c = p.Candidates[i];
                if (c.Refused != null) continue;
                if (p.Mission == null || c.Value > p.Mission.Value) p.Mission = c;
            }
            p.MissionWhy = p.Mission != null
                ? "chose " + p.Mission.Type + " (" + p.Mission.Why + ")"
                : p.Candidates.Count == 0 ? "nothing worth doing" : "nothing passed its gates";
        }

        /// <summary>
        /// Adds a candidate with the gates it has to pass: the player's own launch rules, the purse
        /// above the reserve, and the lead's 10% ceiling on the chance of being caught.
        /// </summary>
        private static void Consider(ModState state, EspionagePlan p, SpyMissionType type, Hero hero, Settlement settlement,
                                     float value, string why)
        {
            var spec = Missions.SpecOf(type);
            var c = new EspionageMissionChoice
            {
                Type = type, Hero = hero, Settlement = settlement, Value = value, Why = why,
                Odds = Missions.OddsOf(state, p.Network, type),
            };
            if (!Missions.CanLaunch(state, p.Owner, p.Target, type, hero, settlement, out var reason)) c.Refused = reason;
            else if (p.Spendable < spec.Gold) c.Refused = "costs " + spec.Gold + ", only " + p.Spendable + " above the reserve";
            else if (c.Odds.Exposure > EspionageConstants.AiMaxExposure)
                c.Refused = "exposure " + Missions.Pct(c.Odds.Exposure) + " is over " + Missions.Pct(EspionageConstants.AiMaxExposure);
            p.Candidates.Add(c);
        }

        // ----- Acting ----------------------------------------------------------

        /// <summary>
        /// Once a week, every realm the player does not rule: plan, then do it through the same calls
        /// the player's levers use. First in <see cref="EspionageUpkeep.Weekly"/>, so budgets set here
        /// are paid, and a handler posted here grows the network, in the same week.
        /// </summary>
        public static void WeeklyTick(ModState state)
        {
            if (state == null) return;

            try
            {
                WindDownOrphans(state);
            }
            catch (Exception ex)
            {
                Log.Error("Espionage", "Winding down orphaned AI networks failed.", ex);
            }

            foreach (var realm in Kingdom.All)
            {
                if (!realm.IsRealm()) continue;
                try
                {
                    var p = Plan(state, realm);
                    if (p.Skip == null) Execute(state, p);
                }
                catch (Exception ex)
                {
                    Log.Error("Espionage", "The espionage AI of " + realm.Name + " failed.", ex);
                }
            }
        }

        /// <summary>
        /// An AI house runs a network only while it rules (decision 5). One that has lost the throne,
        /// or its realm, stops paying and recalls its handler - otherwise a fallen dynasty would fund
        /// its old network for the rest of the campaign with nobody deciding anything about it. The
        /// player's house is never touched: its networks are the player's.
        /// </summary>
        private static void WindDownOrphans(ModState state)
        {
            for (var i = 0; i < state.SpyNetworks.Count; i++)
            {
                var n = state.SpyNetworks[i];
                var owner = n.Owner;
                if (owner == null || owner == Clan.PlayerClan) continue;
                if (owner.Kingdom != null && owner.Kingdom.RulingClan == owner && owner.Kingdom.IsRealm()) continue;
                if (n.WeeklyBudget <= 0 && n.Handler == null) continue;

                if (n.WeeklyBudget > 0) SpyNetworks.SetBudget(state, owner, n.Target, 0);
                if (n.Handler != null) SpyNetworks.Release(state, n, "recalled - " + owner.Name + " no longer rules");
                Log.Info("Espionage", owner.Name + " no longer rules and winds down its network in " + n.Target?.Name + ".");
            }
        }

        private static void Execute(ModState state, EspionagePlan p)
        {
            var realm = p.Realm;

            var standing = CounterIntelligence.BudgetOf(state, realm);
            if ((standing?.WeeklyBudget ?? 0) != p.CounterBudget)
                CounterIntelligence.SetBudget(state, realm, p.CounterBudget, out _);

            // Wind down every network of the house that is not the one planned for: budget to nothing,
            // handler recalled. One network at most (decision 13).
            foreach (var other in SpyNetworks.OwnedBy(state, p.Owner))
            {
                if (other.Target == p.Target) continue;
                if (other.WeeklyBudget > 0) SpyNetworks.SetBudget(state, p.Owner, other.Target, 0);
                if (other.Handler != null) SpyNetworks.Release(state, other, "recalled - " + realm.Name + " looks elsewhere");
            }

            if (p.Target == null || p.Handler == null)
            {
                if (p.Target != null)
                    Log.Debug("Espionage", realm.Name + " would spy on " + p.Target.Name + " but " + p.HandlerWhy + ".");
                return;
            }

            var network = p.Network;
            if (network == null || network.Handler != p.Handler)
            {
                network = SpyNetworks.Assign(state, p.Handler, p.Owner, p.Target, out var refused);
                if (network == null)
                {
                    Log.Info("Espionage", realm.Name + " could not post " + p.Handler.Name + " to " + p.Target.Name + ": " + refused);
                    return;
                }
                Log.Info("Espionage", realm.Name + " sets " + p.Handler.Name + " to spy on " + p.Target.Name + " (" + p.TargetWhy + ").");
            }
            if (network.WeeklyBudget != p.NetworkBudget) SpyNetworks.SetBudget(state, p.Owner, p.Target, p.NetworkBudget);

            if (p.Mission != null)
            {
                var m = Missions.Launch(state, p.Owner, p.Target, p.Mission.Type, p.Mission.Hero, p.Mission.Settlement, out var reason);
                if (m == null) Log.Info("Espionage", realm.Name + " meant to launch " + p.Mission.Type + " and could not: " + reason);
                else Log.Info("Espionage", realm.Name + " launches " + m + " - " + p.Mission.Why + ", exposure " + Missions.Pct(p.Mission.Odds.Exposure) + ".");
            }
        }

        // ----- Reading the world --------------------------------------------------

        /// <summary>
        /// A realm the AI will not spy on: bound to us by a pact, a defensive pact, an alliance or
        /// vassalage. A truce is not a friendship - it is where the next war is planned.
        /// </summary>
        private static bool IsBoundTo(ModState state, Kingdom us, Kingdom them)
        {
            foreach (var t in state.ActiveTreatiesOf(us))
            {
                if (t.Other(us) != them) continue;
                if (t.Type == TreatyType.NonAggressionPact || t.Type == TreatyType.DefensivePact
                    || t.Type == TreatyType.Alliance || t.Type == TreatyType.Vassalage)
                    return true;
            }
            return false;
        }

        /// <summary>The house's network with the most strength - the one worth keeping if there are several.</summary>
        private static SpyNetwork CurrentNetwork(ModState state, Clan owner)
        {
            SpyNetwork best = null;
            foreach (var n in SpyNetworks.OwnedBy(state, owner))
                if (best == null || n.Strength > best.Strength) best = n;
            return best;
        }

        private static bool InCooldown(ModState state, Clan owner, Kingdom target, out float daysLeft)
        {
            daysLeft = 0f;
            for (var i = 0; i < state.SpyMissions.Count; i++)
            {
                var m = state.SpyMissions[i];
                if (m.IsPending || m.Owner != owner || m.Target != target) continue;
                var left = EspionageConstants.AiMissionCooldownDays - (float)(CampaignTime.Now - m.ResolvedOn).ToDays;
                if (left > daysLeft) daysLeft = left;
            }
            return daysLeft > 0f;
        }

        /// <summary>Their town or castle nearest our heartland, with loyalty left to lose.</summary>
        private static Settlement DissentTarget(Kingdom us, Kingdom them)
        {
            var home = us.FactionMidSettlement;
            if (home == null) return null;

            Settlement best = null;
            var bestDistance = float.MaxValue;
            foreach (var fief in them.Fiefs)
            {
                if (fief?.Settlement == null || fief.Loyalty <= EspionageConstants.DissentLoyaltyLoss) continue;
                var d = fief.Settlement.GetPosition2D.Distance(home.GetPosition2D);
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = fief.Settlement;
            }
            return best;
        }

        /// <summary>A town or castle of theirs our own realm is besieging now: sabotage there opens a gate.</summary>
        private static Settlement BesiegedByUs(Kingdom us, Kingdom them)
        {
            foreach (var fief in them.Fiefs)
            {
                var siege = fief?.Settlement?.SiegeEvent;
                if (siege != null && siege.BesiegerCamp?.MapFaction == us) return fief.Settlement;
            }
            return null;
        }

        /// <summary>
        /// The leader of their largest army in the field - the only mark the AI will assassinate
        /// (decision 11). Never their ruler, and never anyone of the player's own house.
        /// </summary>
        private static Hero FieldCommander(Kingdom them)
        {
            Hero best = null;
            var most = 0;
            foreach (var army in them.Armies)
            {
                var leader = army?.LeaderParty?.LeaderHero;
                if (leader == null || !leader.IsAlive || leader == them.Leader) continue;
                if (leader.Clan == Clan.PlayerClan) continue;
                if (army.TotalManCount <= most) continue;
                most = army.TotalManCount;
                best = leader;
            }
            return best;
        }

        private static int RoundDown(int gold) => gold <= 0 ? 0 : gold / 100 * 100;

        // ----- The diagnostic ---------------------------------------------------------

        /// <summary>The plan as text, for <c>diplomacy.ai_espionage</c>.</summary>
        public static string Describe(EspionagePlan p)
        {
            var sb = new StringBuilder();
            sb.AppendLine(p.Realm?.Name + (p.Skip != null ? ": nothing - " + p.Skip : ""));
            if (p.Skip != null) return sb.ToString();

            sb.AppendLine("  purse " + p.Purse + ", " + p.Spendable + " above the reserve of " + EspionageConstants.AiGoldReserve);
            sb.AppendLine("  counter-intelligence: threat " + p.Threat.ToString("0.0") + " (" + p.ThreatWhy + ") -> orders "
                          + p.CounterBudget + "/week");
            for (var i = 0; i < p.Targets.Count; i++)
                sb.AppendLine("  rival " + p.Targets[i].Kingdom.Name + ": " + p.Targets[i].Score.ToString("0") + " (" + p.Targets[i].Why + ")");
            sb.AppendLine("  target: " + (p.Target == null ? "none" : p.Target.Name.ToString()) + " - " + p.TargetWhy);
            if (p.Target != null)
                sb.AppendLine("  handler: " + (p.Handler == null ? "none" : p.Handler.Name.ToString()) + " - " + p.HandlerWhy
                              + "; network " + (p.Network == null ? "not founded" : p.Network.Strength.ToString("0.0"))
                              + ", budget " + p.NetworkBudget + "/week");
            for (var i = 0; i < p.Candidates.Count; i++)
            {
                var c = p.Candidates[i];
                sb.AppendLine("  option " + c.Type + " value " + c.Value.ToString("0.00") + " (" + c.Why + "), success "
                              + Missions.Pct(c.Odds.Success) + ", exposure " + Missions.Pct(c.Odds.Exposure)
                              + (c.Refused != null ? " - REFUSED: " + c.Refused : ""));
            }
            sb.AppendLine("  operation: " + p.MissionWhy);
            return sb.ToString();
        }
    }
}
