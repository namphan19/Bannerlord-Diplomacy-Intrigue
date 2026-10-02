using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace DiplomacyIntrigue.Espionage
{
    /// <summary>How much of a rival one realm is to another, and why.</summary>
    public sealed class EspionageTarget
    {
        public Kingdom Kingdom;
        public float Score;
        public string Why;

        /// <summary>
        /// The net week a network there would have at this week's planned budget, under the
        /// house's best free handler (<see cref="SpyNetworks.ProjectedWeek"/>). Read only when a
        /// new network is being chosen; a network already built is kept on other grounds.
        /// </summary>
        public float Growth;
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

    /// <summary>What the AI is looking for in a rival court: a house at the top of this band first.</summary>
    public enum MarkRead
    {
        /// <summary>A bribe wants the ripest house there is: a defection risk, else merely sullen.</summary>
        ForBribe = 0,
        /// <summary>Forged letters want a sullen house: close to the line, not over it.</summary>
        ForForge = 1,
    }

    /// <summary>
    /// One house the AI would aim a court operation at, and what it read to choose it: the band
    /// that made it a mark and the public sign that picked it inside that band. Both are things the
    /// player can see of that court (story 3.9 R6) - the loyalty figure itself is never in here.
    /// </summary>
    public sealed class CourtMark
    {
        public Clan Clan;
        public Hero Leader;
        public LoyaltyBand Band;
        public string Why;
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

        /// <summary>
        /// How the target's court reads from outside: the crown's band, whether a claimant stands,
        /// whether it is at war with itself. Bands and public facts, never a figure (story 3.9 R1).
        /// </summary>
        public CourtBands.CourtSigns TargetCourt;

        public readonly List<EspionageMissionChoice> Candidates = new List<EspionageMissionChoice>();
        public EspionageMissionChoice Mission;
        public string MissionWhy;
    }

    /// <summary>
    /// The AI's espionage (design 03 §8 step 3.6), under the lead's calls of 2026-09-25 (design 03
    /// §9, decisions 11-13): only a ruling house runs a network, one at most, aimed at a clear rival;
    /// it spends only what its purse can spare; it acts only when being caught is unlikely; and it
    /// assassinates only in war, only a commander in the field, never a ruler.
    ///
    /// **The player's house is a mark like any other** (story 3.10, the lead's decision of
    /// 2026-10-01, replacing decisions 11 and 12's exemptions). Its lords can be bribed, have
    /// letters forged against them and be assassinated under the same rules as every other house,
    /// chosen by the same bands and tie-breaks. Two things stay the player's by design and are
    /// visible: the AI never *plans* against a realm the player rules (<see cref="Plan"/>, line
    /// below), and where an operation would reach the player directly it asks rather than acting
    /// silently - a bribe or a forged letter becomes an offer, in <see cref="Missions"/>. Neither
    /// is a rule about who may be targeted; both are about who gets told.
    ///
    /// **The same rules as the player.** Everything the AI does goes through the calls the player's
    /// levers use - <see cref="SpyNetworks.Assign"/>, <see cref="SpyNetworks.SetBudget"/>,
    /// <see cref="CounterIntelligence.SetBudget"/>, <see cref="Missions.Launch"/> - so every gate,
    /// price and odds is the player's. What is the AI's own is only the choosing.
    ///
    /// **ScoutArmies and ReadCourt are never chosen.** The AI already reads the numbers those two
    /// sell - exhaustion, loyalty, blocs, armies - and paying for a reveal it does not need would be
    /// theatre. That asymmetry predates espionage and is recorded here rather than hidden.
    ///
    /// **What it chooses from, is what the player is shown.** Since story 3.9 (the lead's call,
    /// 2026-10-01) the marks come off the same bands the Encyclopedia draws: the crown through
    /// <see cref="CourtBands.SignsOf"/>, a house through <c>LoyaltyModel.BandOf</c>, and inside a
    /// band only public signs - a great house, our ruler's own relation with it, then the clan's id.
    /// The loyalty figure itself is nowhere on the planning path, because the player is shown four
    /// bands and never the number behind them (design 02 §9.1). Two exact figures of our *own* are
    /// still read, and are named in <see cref="Describe"/> and the class header of
    /// <c>Power</c> rather than here: this realm's purse, and a handler's own skills.
    ///
    /// The player's own realm is never planned for: its espionage is the player's.
    /// </summary>
    public static class AiEspionage
    {
        // ----- Planning --------------------------------------------------------

        public static EspionagePlan Plan(ModState state, Kingdom realm)
        {
            // One of the two places this class still knows the player by name, and the other is
            // <see cref="WindDownOrphans"/>. Both are about the player's *own* espionage, not about
            // who the AI may target - story 3.10 §3 puts both out of its scope, and they follow from
            // decision 5 (the player runs their own networks) rather than from any exemption.
            var p = new EspionagePlan { Realm = realm };
            if (state == null || realm == null || !realm.IsRealm()) { p.Skip = "not a realm"; return p; }

            p.Ruler = realm.Leader;
            p.Owner = realm.RulingClan;
            if (p.Ruler == null || p.Owner == null) { p.Skip = "nobody on the throne"; return p; }
            // The player's own realm is never planned for: its espionage is the player's, and every
            // budget, network and mission in it is one the player ordered.
            if (p.Ruler != null && p.Ruler == Hero.MainHero) { p.Skip = "the player rules it"; return p; }

            p.Purse = p.Ruler.Gold;
            p.Spendable = Math.Max(0, p.Purse - EspionageConstants.AiGoldReserve);

            PlanCounterIntelligence(state, p);
            PlanTarget(state, p);
            PlanNetwork(state, p);

            // How the chosen court reads from outside, whatever the week decides: bands and public
            // facts, so the diagnostic can say what the AI saw even on a week it did nothing
            // (story 3.9 R6). Read here rather than in PlanMission because that returns early on a
            // resting network, and "their court: Secure" would then be a default, not a reading.
            if (p.Target != null) p.TargetCourt = CourtBands.SignsOf(state, p.Target);

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

        /// <summary>
        /// Where the house's one network goes (decision 13: a clear rival). Three rules, the last two
        /// added after run 11 found AI networks at a median 1.4 at year 2 (§8 item 1, the lead
        /// asked the tech lead to choose on 2026-10-02):
        ///
        /// 1. **A network still aimed at a rival stays.** As before.
        /// 2. **A network with real strength stays even when its realm stops scoring as a rival**,
        ///    unless a pact now binds the two. Run 11's networks were founded against whoever the
        ///    house was fighting, and the moment that war ended and the score fell the strength
        ///    built there was thrown away. Decision 4 says a network built before a war is what the
        ///    wartime missions run on; that needs the network to survive the peace before it.
        /// 3. **A new network goes where it can grow.** Among the rivals, highest score first, the
        ///    first where <see cref="SpyNetworks.ProjectedWeek"/> is positive at the budget the house
        ///    would pay. Run 11 aimed new networks at a realm already at war with the house, where the
        ///    war's half rate under 15 counter-intelligence left them shrinking for good. If no rival
        ///    can be grown, the top one is taken as before rather than doing nothing - an idle
        ///    handler there is still a post kept for the day the purse is fuller.
        ///
        /// The war halving itself stays: it is the lead's decision 4, and the same rule for the
        /// player. What changed is where the AI chooses to build.
        /// </summary>
        private static void PlanTarget(ModState state, EspionagePlan p)
        {
            var us = p.Realm;
            var plannedBudget = PlannedNetworkBudget(p);
            var roguery = BestRoguery(state, p.Owner);
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
                p.Targets.Add(new EspionageTarget
                {
                    Kingdom = them, Score = score, Why = why.ToString().TrimEnd(' ', ';'),
                    Growth = SpyNetworks.ProjectedWeek(plannedBudget, roguery, us.IsAtWarWith(them), CounterIntelligence.Of(state, them)),
                });
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

            // Rule 2: strength already built is kept through a peace, unless a pact now binds us.
            if (current != null && current.Strength >= EspionageConstants.AiKeepNetworkStrength
                && current.Target != null && current.Target != us && current.Target.IsRealm()
                && !IsBoundTo(state, us, current.Target))
            {
                p.Target = current.Target;
                p.TargetWhy = "kept: " + current.Strength.ToString("0") + " of strength built there";
                return;
            }

            // Rule 3: a new network goes to the best rival where it can grow.
            EspionageTarget pick = null;
            for (var i = 0; i < p.Targets.Count; i++)
            {
                if (p.Targets[i].Score < EspionageConstants.AiTargetMinScore) break;
                if (p.Targets[i].Growth > 0f) { pick = p.Targets[i]; break; }
            }
            if (pick != null)
            {
                p.Target = pick.Kingdom;
                p.TargetWhy = pick.Why
                              + (pick == p.Targets[0] ? "" : "; " + p.Targets[0].Kingdom.Name + " scores higher, but a network there would shrink")
                              + " (a network would grow " + pick.Growth.ToString("+0.00;-0.00") + " a week)";
            }
            else if (p.Targets.Count > 0 && p.Targets[0].Score >= EspionageConstants.AiTargetMinScore)
            {
                p.Target = p.Targets[0].Kingdom;
                p.TargetWhy = p.Targets[0].Why + " (no rival where a network could grow; "
                              + p.Targets[0].Growth.ToString("+0.00;-0.00") + " a week here)";
            }
            else p.TargetWhy = "no clear rival";
        }

        /// <summary>What the house would put into its network this week: a share of the purse above the reserve, capped.</summary>
        private static int PlannedNetworkBudget(EspionagePlan p)
            => RoundDown(Math.Min((int)(p.Spendable * EspionageConstants.AiNetworkBudgetShare), EspionageConstants.AiNetworkBudgetCap));

        /// <summary>
        /// The roguery of the hero the house would put on a new network: its current handler if it
        /// has one, else the best of the members free to go. Roguery is what the growth term reads;
        /// the handler is then chosen by ceiling in <see cref="PlanNetwork"/>, which leans on roguery
        /// (roguery / 2 against charm / 4), so the two usually name the same hero.
        /// </summary>
        private static float BestRoguery(ModState state, Clan owner)
        {
            var best = 0f;
            foreach (var n in SpyNetworks.OwnedBy(state, owner))
                if (n.Handler != null) best = Math.Max(best, n.Handler.GetSkillValue(DefaultSkills.Roguery));
            foreach (var hero in owner.Heroes)
                if (SpyNetworks.IsFreeToGo(hero, owner, out _))
                    best = Math.Max(best, hero.GetSkillValue(DefaultSkills.Roguery));
            return best;
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
                p.NetworkBudget = PlannedNetworkBudget(p);
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

            // Bribes and forgeries aim at a civil war, and only a shaky crown has one coming. Read
            // through the bands the Encyclopedia shows rather than the exact legitimacy (story 3.9
            // R1): Questioned or Failing, or a claimant standing, which is announced either way.
            // Same set as the figure it replaced - LegitimacyNeutral is the Questioned edge - so
            // this changes what the AI knows, not who it looks at (AC4).
            var signs = p.TargetCourt;
            var shaky = signs.Crown != CrownStanding.Secure || signs.PretenderStands;
            if (shaky)
            {
                // The bribe mark: the ripest band there is, and inside it the house a foreign court
                // would name first (story 3.9 R2, R4). Public signs only, so the choice is one the
                // player could have made from what they can see of that court.
                var bribe = MarkOf(state, us, them, MarkRead.ForBribe);
                if (bribe != null)
                    Consider(state, p, SpyMissionType.BribeLord, bribe.Leader, null,
                             EspionageConstants.AiBribeScore(bribe.Band), bribe.Why);

                // The forgery mark: the Disaffected band only - close enough to the defection line
                // that a grievance of 8 (x1.5 on loyalty) can matter, and not over it yet. The old
                // range ran to 45, five points into Transactional, where a letter had nothing to
                // tip (story 3.9 R3).
                //
                // The player's house is not excluded: it is chosen by this same band rule as every
                // other house, and what a success does there is story 3.10's offer.
                var forge = MarkOf(state, us, them, MarkRead.ForForge);
                if (forge != null)
                    Consider(state, p, SpyMissionType.ForgeLetters, forge.Leader, null, 1.5f, forge.Why);
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
        /// The house this realm would aim a court operation at, chosen from bands and public signs
        /// only (story 3.9 R2, R4, R7). The loyalty figure decides which band a house is in and
        /// nothing else; inside the band the three tie-breaks are what an envoy could report.
        /// </summary>
        private static CourtMark MarkOf(ModState state, Kingdom us, Kingdom them, MarkRead read)
        {
            var wanted = read == MarkRead.ForBribe
                ? new[] { LoyaltyBand.DefectionRisk, LoyaltyBand.Disaffected }
                : new[] { LoyaltyBand.Disaffected };

            foreach (var band in wanted)
            {
                var best = BestInBand(state, us, them, band, read, out var tie);
                if (best == null) continue;
                return new CourtMark
                {
                    Clan = best, Leader = best.Leader, Band = band,
                    Why = best.Name + " of " + them.Name + " is " + CourtBands.MoodName(band).ToLowerInvariant() + " - " + tie,
                };
            }
            return null;
        }

        /// <summary>
        /// The best house of one band, by story 3.9's tie-break order: a great house first (more men
        /// to take to a rising), then the house our own ruler gets on best with - we know our own
        /// relations exactly - then the clan's string id, so the same court gives the same answer
        /// every week and a log line can be matched against a save. The id last rather than first
        /// because a stable choice is worth less than a sensible one: it only breaks ties the two
        /// real signs left even.
        /// </summary>
        private static Clan BestInBand(ModState state, Kingdom us, Kingdom them, LoyaltyBand band, MarkRead read, out string why)
        {
            Clan best = null;
            var bestWeight = CourtWeight.NoWeight;
            var bestRelation = int.MinValue;
            var rivals = 0;
            string whyTie = null;
            why = null;

            foreach (var clan in Court.MembersOf(them))
            {
                if (clan == them.RulingClan || clan.Leader == null) continue;
                if (LoyaltyModel.BandOf(state, clan) != band) continue;
                // A house already bought is bought: another purse buys a longer window, not a second
                // turn of its loyalty, so a second bribe on it is gold spent for nothing. The same
                // reason Bribes.IsBought is the resolver loyalty reads.
                if (read == MarkRead.ForBribe && Bribes.IsBought(state, clan)) continue;

                var weight = CourtBands.WeightOf(clan, them);
                var relation = us.Leader != null && clan.Leader != null
                    ? FactionManager.GetRelationBetweenClans(us.RulingClan, clan) : 0;

                // Which sign actually decided it, so the reason printed is the one that acted
                // (story 3.9 R6) rather than a list of everything that was compared.
                string wonBy = null;
                if (best == null) wonBy = null;
                else if (weight > bestWeight) wonBy = "it weighs more in that court than any other";
                else if (weight == bestWeight && relation > bestRelation) wonBy = RelationNote(relation);
                else if (weight == bestWeight && relation == bestRelation
                         && string.CompareOrdinal(clan.StringId, best.StringId) < 0)
                    wonBy = "the other houses compared equal, and this one is named first";
                else continue;

                if (best != null) rivals++;
                if (wonBy != null) whyTie = wonBy;
                best = clan;
                bestWeight = weight;
                bestRelation = relation;
            }

            if (best == null) return null;

            // A single house in the band needed no tie-break at all, and claiming one was applied
            // would be the diagnostic lying about why.
            why = CourtBands.Name(bestWeight).ToLowerInvariant()
                  + (bestWeight == CourtWeight.GreatHouse ? " - the most men to take to a rising" : "")
                  + " (" + best.StringId + ")";
            if (rivals > 0 && whyTie != null) why += ", chosen because " + whyTie;
            return best;
        }

        /// <summary>
        /// How our ruler stands with a house, in words rather than a number. A relation is public -
        /// the map and the hero page both show it - but a signed integer in a diagnostic reads as
        /// precision the reader does not have, and the order is what decides the choice.
        /// </summary>
        private static string RelationNote(int relation)
        {
            if (relation >= 40) return "and our ruler counts them a friend";
            if (relation <= -40) return "and our ruler is hostile to them";
            return "and our ruler is indifferent to them";
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
        /// its old network for the rest of the campaign with nobody deciding anything about it.
        ///
        /// The player's house is skipped here for the same reason <see cref="Plan"/> does not plan
        /// against a realm the player rules: these networks are the player's, bought and spent by
        /// them, and a rule that recalled their own handler because they had lost a throne they never
        /// held would be taking their espionage away from them. The second of the two places this
        /// class knows the player by name; story 3.10 §3 puts both out of its scope.
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
                if (n.Handler != null) SpyNetworks.Release(state, n, HandlerLossCause.Recalled, owner.Name + " no longer rules");
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
                if (other.Handler != null) SpyNetworks.Release(state, other, HandlerLossCause.Recalled, realm.Name + " looks elsewhere");
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
        /// (decision 11). Never their ruler.
        ///
        /// **The player's house is no longer exempt** (story 3.10 R1, the lead's decision of
        /// 2026-10-01). It used to be, and the reason it was - that the player could not be made
        /// to lose a character to a roll they never saw - is answered a different way: the AI's
        /// odds are the player's own (<see cref="Missions.OddsOf"/>), the mark is the leader of an
        /// army the player can see on the map, and a success is announced to the victim's side
        /// (<see cref="Missions.TellVictim"/>). The exemption was the only house in Calradia the
        /// AI could not touch, which is the asymmetry this pillar exists to remove.
        ///
        /// The player hero is therefore a mark like any other lord, when they command that army and
        /// do not rule the realm: a ruler is never a mark, whoever they are.
        /// </summary>
        private static Hero FieldCommander(Kingdom them)
        {
            Hero best = null;
            var most = 0;
            foreach (var army in them.Armies)
            {
                var leader = army?.LeaderParty?.LeaderHero;
                if (leader == null || !leader.IsAlive || leader == them.Leader) continue;
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
                sb.AppendLine("  rival " + p.Targets[i].Kingdom.Name + ": " + p.Targets[i].Score.ToString("0") + " (" + p.Targets[i].Why
                              + "), a network there " + p.Targets[i].Growth.ToString("+0.00;-0.00") + " a week");
            sb.AppendLine("  target: " + (p.Target == null ? "none" : p.Target.Name.ToString()) + " - " + p.TargetWhy);
            if (p.Target != null)
            {
                var signs = p.TargetCourt;
                sb.AppendLine("  their court: crown " + CourtBands.Name(signs.Crown)
                              + (signs.PretenderStands ? ", a claimant stands" : "")
                              + (signs.AtWarWithItself ? ", at war with itself" : "")
                              + " - worth subverting: " + (signs.Crown != CrownStanding.Secure || signs.PretenderStands));
                sb.AppendLine("  handler: " + (p.Handler == null ? "none" : p.Handler.Name.ToString()) + " - " + p.HandlerWhy
                              + "; network " + (p.Network == null ? "not founded" : p.Network.Strength.ToString("0.0"))
                              + ", budget " + p.NetworkBudget + "/week");
            }
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
