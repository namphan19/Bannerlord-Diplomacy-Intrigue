using System;
using System.Text;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// The engine that gives wars an ending condition.
    ///
    /// Two numbers move here, and they answer different questions:
    ///   exhaustion - how much longer can this side keep fighting?  (drives willingness)
    ///   war score  - who is actually winning?                      (drives peace terms)
    ///
    /// All entry points are static and take the state explicitly, so the rules stay
    /// testable and free of campaign lookups. Callers are behaviors, which own the
    /// event wiring and the exception handling.
    /// </summary>
    public static class WarExhaustion
    {
        /// <summary>
        /// Per-day accrual for every ongoing war, plus the daily decay of peacetime
        /// weariness. Called once per campaign day.
        /// </summary>
        /// <summary>
        /// How worn down a realm is by the worst war it is currently fighting. The realm's
        /// war exhaustion, wherever anything asks for a single number per kingdom.
        ///
        /// Promoted from two byte-identical private copies in <see cref="AiDiplomacy"/> and
        /// <see cref="CallToArms"/> when court loyalty needed a third. They happened to agree,
        /// but CLAUDE.md §3 is explicit that a value derived in more than one place *is* the
        /// bug - legitimacy was computed twice, twice, and both times a kingdom honouring a
        /// treaty was punished as an aggressor.
        /// </summary>
        public static float Worst(ModState state, Kingdom kingdom)
        {
            if (state == null || kingdom == null) return 0f;

            var worst = 0f;
            foreach (var war in state.OngoingWarsOf(kingdom))
            {
                var value = war.ExhaustionOf(kingdom);
                if (value > worst) worst = value;
            }
            return worst;
        }

        public static void DailyTick(ModState state)
        {
            var rate = Settings.Current.WarExhaustionRate;

            for (var i = 0; i < state.Wars.Count; i++)
            {
                var war = state.Wars[i];
                if (!war.IsOngoing) continue;

                // Time alone wears both sides down. This is what guarantees that even a
                // perfectly balanced stalemate eventually ends.
                var perDay = DiplomacyConstants.ExhaustionPerDayAtWar * rate;
                Accrue(war, war.Aggressor, perDay);
                Accrue(war, war.Defender, perDay);

                // Losing ground keeps hurting for as long as the enemy holds it.
                Accrue(war, war.Aggressor,
                    war.FiefsTakenByDefender * DiplomacyConstants.ExhaustionPerDayPerOccupiedFief * rate);
                Accrue(war, war.Defender,
                    war.FiefsTakenByAggressor * DiplomacyConstants.ExhaustionPerDayPerOccupiedFief * rate);

                ApplySiegePressure(war, war.Aggressor, war.Defender, rate);
                ApplySiegePressure(war, war.Defender, war.Aggressor, rate);

                // Victories fade: a stalemate drifts back toward a white peace.
                WarScore.Decay(war);
            }

            DecayWeariness(state);
        }

        /// <summary>
        /// Adds exhaustion to <paramref name="besieged"/> for each of its fortifications
        /// currently invested by <paramref name="besieger"/>.
        /// </summary>
        private static void ApplySiegePressure(WarRecord war, Kingdom besieged, Kingdom besieger, float rate)
        {
            var settlements = besieged.Settlements;
            for (var i = 0; i < settlements.Count; i++)
            {
                var settlement = settlements[i];
                if (!settlement.IsFortification) continue;

                var siege = settlement.SiegeEvent;
                if (siege == null) continue;
                if (siege.BesiegerCamp?.MapFaction != besieger) continue;

                Accrue(war, besieged, DiplomacyConstants.ExhaustionPerDayUnderSiege * rate);
            }
        }

        /// <summary>
        /// Battle result: casualties become exhaustion, and the share of each side's army lost
        /// becomes war score (design 10 §3).
        /// </summary>
        public static void ApplyBattleResult(ModState state, MapEvent mapEvent)
        {
            var attacker = mapEvent.AttackerSide?.MapFaction as Kingdom;
            var defender = mapEvent.DefenderSide?.MapFaction as Kingdom;
            if (attacker == null || defender == null || attacker == defender) return;

            var war = state.OngoingWarBetween(attacker, defender);
            if (war == null) return;

            // Exhaustion keeps the engine's counter, which includes the wounded: a wounded
            // army is a tired army even when it is back in the ranks within days.
            var attackerLosses = mapEvent.AttackerSide.TroopCasualties;
            var defenderLosses = mapEvent.DefenderSide.TroopCasualties;

            AddCasualtyExhaustion(war, attacker, attackerLosses);
            AddCasualtyExhaustion(war, defender, defenderLosses);

            if (ScoresAsBattle(mapEvent.EventType))
                AddBattleWarScore(war, mapEvent, attacker, defender);
        }

        /// <summary>
        /// Every fight between armies scores, sieges and sally-outs included (design 10, F5).
        /// A raid, a forced levy or a hideout is not one: the men on the other side are villagers,
        /// militia or bandits, not the realm's army.
        /// </summary>
        private static bool ScoresAsBattle(MapEvent.BattleTypes type)
        {
            switch (type)
            {
                case MapEvent.BattleTypes.FieldBattle:
                case MapEvent.BattleTypes.Siege:
                case MapEvent.BattleTypes.SallyOut:
                case MapEvent.BattleTypes.SiegeOutside:
                case MapEvent.BattleTypes.SiegeAmbush:
                case MapEvent.BattleTypes.BlockadeBattle:
                case MapEvent.BattleTypes.BlockadeSallyOutBattle:
                    return true;
                default:
                    return false;
            }
        }

        private static void AddCasualtyExhaustion(WarRecord war, Kingdom kingdom, int losses)
        {
            if (losses <= 0) return;
            war.AddCasualties(kingdom, losses);

            // Relative to what the kingdom can field - see ExhaustionCasualtyStrengthDivisor.
            var divisor = Math.Max(
                DiplomacyConstants.ExhaustionCasualtyMinDivisor,
                kingdom.CurrentTotalStrength / DiplomacyConstants.ExhaustionCasualtyStrengthDivisor);

            Accrue(war, kingdom, losses / divisor * Settings.Current.WarExhaustionRate);
        }

        /// <summary>
        /// <c>W × lostShare(defender) − W × lostShare(attacker)</c>, capped, plus the win award,
        /// written aggressor-positive. Only each kingdom's own lord parties and garrisons count:
        /// an ally's men who joined the battle are not this realm's army and are not measured
        /// against its manpower.
        /// </summary>
        private static void AddBattleWarScore(WarRecord war, MapEvent mapEvent, Kingdom attacker, Kingdom defender)
        {
            WarScore.EnsureManpower(war);

            var attackerParties = new StringBuilder();
            var defenderParties = new StringBuilder();
            CountLosses(mapEvent, mapEvent.AttackerSide, attacker, out var attackerLost, out var attackerFielded, attackerParties);
            CountLosses(mapEvent, mapEvent.DefenderSide, defender, out var defenderLost, out var defenderFielded, defenderParties);

            // Positive: the battle went the attacker's way.
            var proportional = DiplomacyConstants.WarScoreLossShareWeight
                               * (Share(defenderLost, war.ManpowerAtStartOf(defender))
                                  - Share(attackerLost, war.ManpowerAtStartOf(attacker)));
            proportional = Clamp(proportional, -DiplomacyConstants.WarScoreBattleCap, DiplomacyConstants.WarScoreBattleCap);

            var award = 0f;
            var winning = mapEvent.WinningSide;
            if (winning == mapEvent.AttackerSide.MissionSide && defenderFielded >= DiplomacyConstants.WarScoreWinMinMen)
                award = DiplomacyConstants.WarScoreWinPoints;
            else if (winning == mapEvent.DefenderSide.MissionSide && attackerFielded >= DiplomacyConstants.WarScoreWinMinMen)
                award = -DiplomacyConstants.WarScoreWinPoints;

            var points = proportional + award;
            if (points == 0f) return;
            war.AddBattleScore(attacker == war.Aggressor ? points : -points);

            // One line per scored battle, so a live check can read each term against design 10
            // §3 by hand and a balance run can see what the battles were worth. The party lists
            // give every party on each side as name:kind:faction:counted:men:died[:gone] (see
            // CountLosses), and settlementNow is who holds the battle's settlement as the event
            // ends - together they settle design 10 §9a's siege whose defender "fielded 0".
            var settlement = mapEvent.MapEventSettlement;
            Telemetry.Event("battle_scored", "type", mapEvent.EventType,
                "attacker", attacker, "defender", defender,
                "winner", winning == mapEvent.AttackerSide.MissionSide ? "attacker"
                          : winning == mapEvent.DefenderSide.MissionSide ? "defender" : "none",
                "attackerLost", attackerLost, "attackerFielded", attackerFielded,
                "attackerManpower", war.ManpowerAtStartOf(attacker),
                "defenderLost", defenderLost, "defenderFielded", defenderFielded,
                "defenderManpower", war.ManpowerAtStartOf(defender),
                "proportional", proportional, "award", award,
                "battleScore", war.BattleScore,
                "attackerSideMen", SideMen(mapEvent.AttackerSide), "defenderSideMen", SideMen(mapEvent.DefenderSide),
                "settlement", settlement?.Name?.ToString(),
                "settlementNow", settlement?.MapFaction?.Name?.ToString(),
                "attackerParties", attackerParties.ToString(), "defenderParties", defenderParties.ToString());
        }

        /// <summary>
        /// Every man on a side at the start, counted or not: militia, allies and the settlement's
        /// own party included. Telemetry only, so a battle that scored a side as fielding nobody
        /// shows whether it truly had no army there or its men were excluded.
        /// </summary>
        private static int SideMen(MapEventSide side)
        {
            var men = 0;
            if (side == null) return men;
            var parties = side.Parties;
            for (var i = 0; i < parties.Count; i++)
                men += parties[i]?.HealthyManCountAtStart ?? 0;
            return men;
        }

        /// <summary>
        /// Men a kingdom lost in a battle, and how many it fielded. Lost means killed or taken:
        /// a side that was defeated and did not get away has every man it brought captured by
        /// the time this runs (vanilla's <c>CaptureDefeatedPartyMembers</c> empties the rosters
        /// before <c>MapEventEnded</c> fires, verified by IL on v1.4.8), so its loss is everyone
        /// healthy at the start; otherwise only its dead. The wounded of a side that holds the
        /// field are back in the ranks in days and are not lost (design 10 D3).
        ///
        /// <c>HealthyManCountAtStart</c> includes a party's heroes, a handful per party; they
        /// are left in rather than guessed out, since the rosters are already emptied.
        ///
        /// Which realm a party fought for is its live map faction, with one exception (design 10
        /// §9a, 2026-09-27): the garrison of a fortress taken by assault. A garrison's map faction
        /// follows whoever holds its walls, while the side's own faction - which is what
        /// <paramref name="kingdom"/> is - was fixed when the battle began. If the fortress has
        /// already changed hands when <c>MapEventEnded</c> fires, its garrison reads as the
        /// captor's, is counted on neither side, and the defender "fielded 0" - the one way the
        /// code lets a real garrison vanish from the count. Whether vanilla does change the owner
        /// before the event is not known here (no method bodies in the reference assemblies), so
        /// a garrison of the assaulted fortress that reads as the attacker's is counted for the
        /// side that held the walls (<see cref="HeldItsOwnWalls"/>). When the owner has not
        /// changed yet, the garrison reads as the defender's and is counted as it always was: the
        /// rule changes nothing. <paramref name="detail"/> records every party, so the next
        /// battle_scored line shows which case it was: <c>yes(walls)</c> on a garrison is the
        /// capture coming first.
        /// </summary>
        private static void CountLosses(MapEvent mapEvent, MapEventSide side,
            Kingdom kingdom, out int lost, out int fielded, StringBuilder detail)
        {
            lost = 0;
            fielded = 0;
            if (side == null) return;

            var takenWhole = mapEvent.DefeatedSide == side.MissionSide && mapEvent.RetreatingSide != side.MissionSide;

            var parties = side.Parties;
            for (var i = 0; i < parties.Count; i++)
            {
                var entry = parties[i];
                if (entry?.Party == null) continue;
                var party = entry.Party.MobileParty;

                string counted;
                if (party == null || !(party.IsLordParty || party.IsGarrison)) counted = "no(army)";
                else if (party.MapFaction == kingdom) counted = "yes";
                else if (HeldItsOwnWalls(mapEvent, side, party)) counted = "yes(walls)";
                else counted = "no(realm)";

                var partyLost = takenWhole ? entry.HealthyManCountAtStart : (entry.DiedInBattle?.TotalRegulars ?? 0);
                if (counted.StartsWith("yes", StringComparison.Ordinal))
                {
                    fielded += entry.HealthyManCountAtStart;
                    lost += partyLost;
                }

                if (detail == null) continue;
                if (detail.Length > 0) detail.Append('/');
                detail.Append(entry.Party.Name?.ToString() ?? "?").Append(':')
                      .Append(KindOf(entry.Party)).Append(':')
                      .Append(entry.Party.MapFaction?.Name?.ToString() ?? "none").Append(':')
                      .Append(counted).Append(':')
                      .Append(entry.HealthyManCountAtStart).Append(':')
                      .Append(entry.DiedInBattle?.TotalRegulars ?? 0);
                if (party != null && !party.IsActive) detail.Append(":gone");
            }
        }

        /// <summary>
        /// A garrison on the defending side of an assault on its own fortress that already answers
        /// to the attacker: the walls changed hands before this event fired, and it fought for
        /// the realm that held them when the assault began, the side's own. Requiring the
        /// attacker's faction, not just any other one, keeps the rule to that capture: a garrison
        /// that reads as some third realm is left uncounted, as before.
        /// </summary>
        private static bool HeldItsOwnWalls(MapEvent mapEvent, MapEventSide side, MobileParty party)
            => party.IsGarrison
               && mapEvent.IsSiegeAssault
               && side == mapEvent.DefenderSide
               && mapEvent.MapEventSettlement != null
               && party.GarrisonPartyComponent?.Settlement == mapEvent.MapEventSettlement
               && party.MapFaction != null
               && party.MapFaction == mapEvent.AttackerSide?.MapFaction;

        /// <summary>What a party on a battle side is, for the battle_scored party lists.</summary>
        private static string KindOf(PartyBase party)
        {
            var mobile = party.MobileParty;
            if (mobile == null) return party.IsSettlement ? "settlement" : "other";
            if (mobile.IsLordParty) return "lord";
            if (mobile.IsGarrison) return "garrison";
            if (mobile.IsMilitia) return "militia";
            if (mobile.IsPatrolParty) return "patrol";
            if (mobile.IsVillager) return "villager";
            if (mobile.IsCaravan) return "caravan";
            if (mobile.IsBandit) return "bandit";
            return "other";
        }

        private static float Share(int lost, int manpower)
            => manpower > 0 ? (float)lost / manpower : 0f;

        /// <summary>
        /// A fortification changed hands. The former owner takes exhaustion. No war score: the
        /// captor keeps the fief at the peace, and scoring it too would pay for it twice (design
        /// 10, F7). The fighting that took it scored through the siege battle.
        /// </summary>
        public static void ApplyFiefCapture(ModState state, Settlement settlement,
            Kingdom captor, Kingdom formerOwner)
        {
            if (settlement == null || captor == null || formerOwner == null || captor == formerOwner) return;
            if (!settlement.IsFortification) return;

            var war = state.OngoingWarBetween(captor, formerOwner);
            if (war == null) return;

            var exhaustion = settlement.IsTown
                ? DiplomacyConstants.ExhaustionPerTownLost
                : DiplomacyConstants.ExhaustionPerCastleLost;

            Accrue(war, formerOwner, exhaustion * Settings.Current.WarExhaustionRate);
            war.AddFiefCapture(captor);
        }

        /// <summary>
        /// A village was looted. Small on its own; attrition when repeated. No war score: a raid
        /// is not a battle between armies (design 10 §3).
        /// </summary>
        public static void ApplyVillageRaided(ModState state, Village village, Kingdom raider)
        {
            var owner = village?.Settlement?.MapFaction as Kingdom;
            if (owner == null || raider == null || owner == raider) return;

            var war = state.OngoingWarBetween(raider, owner);
            if (war == null) return;

            Accrue(war, owner, DiplomacyConstants.ExhaustionPerVillageRaided * Settings.Current.WarExhaustionRate);
        }

        /// <summary>
        /// Called when a war closes: part of each side's exhaustion becomes lasting
        /// weariness, so the peace has weight even after the war record goes quiet.
        /// </summary>
        public static void CarryOverToWeariness(ModState state, WarRecord war)
        {
            // A war nobody chose leaves no political hangover. Obligation wars begin and
            // end with the principal's, often within days, and in run 01 there were 92 of
            // them - each quietly adding weariness for a war its participant never wanted.
            if (war.IsObligationWar) return;

            var fraction = DiplomacyConstants.WearinessCarryOverFraction;
            Carry(state, war.Aggressor, war.AggressorExhaustion * fraction);
            Carry(state, war.Defender, war.DefenderExhaustion * fraction);
        }

        /// <summary>
        /// Adds weariness only if the war left a real mark. Below the minimum it is noise,
        /// and noise that accumulates is what turned a temporary brake into a permanent one.
        /// </summary>
        private static void Carry(ModState state, Kingdom kingdom, float amount)
        {
            if (amount < DiplomacyConstants.WearinessCarryOverMinimum) return;
            state.AddWeariness(kingdom, amount);
        }

        /// <summary>
        /// Sheds a fraction of each pool rather than a fixed amount. See
        /// <see cref="DiplomacyConstants.WearinessDecayFractionPerDay"/> for why: a flat
        /// drain cannot bound a pool that keeps being topped up, and in a measured 28-year
        /// run every kingdom ended pinned above 80.
        /// </summary>
        private static void DecayWeariness(ModState state)
        {
            for (var i = state.Weariness.Count - 1; i >= 0; i--)
            {
                var entry = state.Weariness[i];

                var shed = entry.Value * DiplomacyConstants.WearinessDecayFractionPerDay;
                if (shed < DiplomacyConstants.WearinessDecayMinimumPerDay)
                    shed = DiplomacyConstants.WearinessDecayMinimumPerDay;

                entry.Decay(shed);
                if (entry.Value <= 0f) state.Weariness.RemoveAt(i);
            }
        }

        /// <summary>
        /// Every accrual goes through here, so no source escapes the ruler's resolve (design 08
        /// S-1): a realm led by a ruler its vassals follow tires more slowly. The enemy's reading
        /// of the band is untouched - it reads the value, not the rate.
        /// </summary>
        private static void Accrue(WarRecord war, Kingdom kingdom, float amount)
            => war.AddExhaustion(kingdom, amount * Statecraft.StatecraftTerms.ResolveFactor(kingdom));

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
