using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
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

                // Pull an idle war score back toward a white peace.
                Drift(war);
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

        private static void Drift(WarRecord war)
        {
            var score = war.WarScore;
            if (score > 0f)
                war.AddWarScore(-Math.Min(DiplomacyConstants.WarScoreDriftPerDay, score));
            else if (score < 0f)
                war.AddWarScore(Math.Min(DiplomacyConstants.WarScoreDriftPerDay, -score));
        }

        /// <summary>
        /// Battle result: casualties become exhaustion, the casualty differential becomes
        /// war score. Raids and sieges are handled by the fief-specific entry points, so
        /// this only looks at the fighting itself.
        /// </summary>
        public static void ApplyBattleResult(ModState state, MapEvent mapEvent)
        {
            var attacker = mapEvent.AttackerSide?.MapFaction as Kingdom;
            var defender = mapEvent.DefenderSide?.MapFaction as Kingdom;
            if (attacker == null || defender == null || attacker == defender) return;

            var war = state.OngoingWarBetween(attacker, defender);
            if (war == null) return;

            var attackerLosses = mapEvent.AttackerSide.TroopCasualties;
            var defenderLosses = mapEvent.DefenderSide.TroopCasualties;

            AddCasualtyExhaustion(war, attacker, attackerLosses);
            AddCasualtyExhaustion(war, defender, defenderLosses);

            // Sieges and raids move the score through fief capture / raid handlers, and
            // counting the assault losses again here would double-dip.
            if (mapEvent.EventType == MapEvent.BattleTypes.FieldBattle)
                AddBattleWarScore(war, attacker, defender, attackerLosses, defenderLosses);
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

        private static void AddBattleWarScore(WarRecord war, Kingdom attacker, Kingdom defender,
            int attackerLosses, int defenderLosses)
        {
            var total = Math.Max(DiplomacyConstants.WarScoreBattleMinTotal, (float)(attackerLosses + defenderLosses));
            var differential = defenderLosses - attackerLosses;
            if (differential == 0) return;

            var magnitude = Math.Abs(differential) / total * DiplomacyConstants.WarScoreBattleFactor;
            magnitude = Clamp(magnitude, DiplomacyConstants.WarScoreBattleMin, DiplomacyConstants.WarScoreBattleMax);

            // Whoever inflicted more losses gained ground; express it aggressor-positive.
            var beneficiary = differential > 0 ? attacker : defender;
            war.AddWarScore(beneficiary == war.Aggressor ? magnitude : -magnitude);
        }

        /// <summary>
        /// A fortification changed hands. The former owner takes exhaustion, the captor
        /// takes war score.
        /// </summary>
        public static void ApplyFiefCapture(ModState state, Settlement settlement,
            Kingdom captor, Kingdom formerOwner)
        {
            if (settlement == null || captor == null || formerOwner == null || captor == formerOwner) return;
            if (!settlement.IsFortification) return;

            var war = state.OngoingWarBetween(captor, formerOwner);
            if (war == null) return;

            var isTown = settlement.IsTown;
            var exhaustion = isTown
                ? DiplomacyConstants.ExhaustionPerTownLost
                : DiplomacyConstants.ExhaustionPerCastleLost;
            var score = isTown
                ? DiplomacyConstants.WarScorePerTownCaptured
                : DiplomacyConstants.WarScorePerCastleCaptured;

            Accrue(war, formerOwner, exhaustion * Settings.Current.WarExhaustionRate);
            war.AddFiefCapture(captor);
            war.AddWarScore(captor == war.Aggressor ? score : -score);
        }

        /// <summary>A village was looted. Small on its own; attrition when repeated.</summary>
        public static void ApplyVillageRaided(ModState state, Village village, Kingdom raider)
        {
            var owner = village?.Settlement?.MapFaction as Kingdom;
            if (owner == null || raider == null || owner == raider) return;

            var war = state.OngoingWarBetween(raider, owner);
            if (war == null) return;

            Accrue(war, owner, DiplomacyConstants.ExhaustionPerVillageRaided * Settings.Current.WarExhaustionRate);
            war.AddWarScore(raider == war.Aggressor
                ? DiplomacyConstants.WarScorePerVillageRaided
                : -DiplomacyConstants.WarScorePerVillageRaided);
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
