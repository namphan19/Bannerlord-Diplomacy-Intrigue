using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// The war score, and the only place it is resolved (design 10): the battle part stored on
    /// the war record, plus the prisoners each side holds, read live. Everything that shows or
    /// weighs a war score - the peace table, the AI, the Realm tab, telemetry - reads it here,
    /// so a number shown is the number the AI used (CLAUDE.md §3, one resolver per concept).
    /// </summary>
    public static class WarScore
    {
        /// <summary>The war score from <paramref name="kingdom"/>'s side: positive when it is ahead.</summary>
        public static float For(WarRecord war, Kingdom kingdom)
        {
            if (war == null || kingdom == null) return 0f;
            if (kingdom == war.Aggressor) return Total(war);
            if (kingdom == war.Defender) return -Total(war);
            return 0f;
        }

        /// <summary>Aggressor-positive, as the war record stores its battle part.</summary>
        public static float Total(WarRecord war)
        {
            if (war == null) return 0f;
            return war.BattleScore + Prisoners(war.Aggressor, war.Defender) - Prisoners(war.Defender, war.Aggressor);
        }

        /// <summary>
        /// Points for the lords of <paramref name="victim"/> that <paramref name="holder"/> holds
        /// now. A state, not an event: a lord counts while held and stops the day he is ransomed,
        /// released or escapes. Vanilla frees many lords within days of a battle, and an event
        /// with a decay would have paid again for every capture-and-escape.
        /// </summary>
        public static float Prisoners(Kingdom holder, Kingdom victim)
        {
            var total = 0f;
            foreach (var hero in HeldLords(holder, victim))
                total += PrisonerWeight(victim, hero);
            return total;
        }

        /// <summary>
        /// The lords of <paramref name="victim"/> held by <paramref name="holder"/>. Lords of noble
        /// clans only: a mercenary's hero is not the realm's great man, and a minor clan in its
        /// pay is not its court.
        /// </summary>
        public static IEnumerable<Hero> HeldLords(Kingdom holder, Kingdom victim)
        {
            if (holder == null || victim == null) yield break;
            var lords = victim.AliveLords;
            for (var i = 0; i < lords.Count; i++)
            {
                var hero = lords[i];
                if (hero == null || !hero.IsPrisoner) continue;
                var clan = hero.Clan;
                if (clan == null || clan.IsUnderMercenaryService || clan.IsMinorFaction) continue;
                if (hero.PartyBelongedToAsPrisoner?.MapFaction != holder) continue;
                yield return hero;
            }
        }

        public static float PrisonerWeight(Kingdom victim, Hero hero)
        {
            if (hero == victim.Leader) return DiplomacyConstants.WarScorePrisonerRuler;
            if (hero.Clan != null && hero.Clan.Leader == hero) return DiplomacyConstants.WarScorePrisonerClanLeader;
            return DiplomacyConstants.WarScorePrisonerLord;
        }

        /// <summary>
        /// Men in the kingdom's lord parties and garrisons, heroes excluded (they count as
        /// prisoners, not as men). Militia, villagers, caravans and patrols are not the realm's
        /// army; a town the realm loses already costs it exhaustion.
        /// </summary>
        public static int Manpower(Kingdom kingdom)
        {
            if (kingdom == null) return 0;
            var men = 0;

            var parties = kingdom.WarPartyComponents;
            for (var i = 0; i < parties.Count; i++)
            {
                var party = parties[i]?.MobileParty;
                if (party == null || !party.IsLordParty) continue;
                men += party.MemberRoster?.TotalRegulars ?? 0;
            }

            var fiefs = kingdom.Fiefs;
            for (var i = 0; i < fiefs.Count; i++)
                men += fiefs[i]?.GarrisonParty?.MemberRoster?.TotalRegulars ?? 0;

            return men;
        }

        /// <summary>
        /// Records each side's manpower if it is not recorded yet: at the war's opening, and once
        /// for a war loaded from a save that predates the fields - read live then, which is the
        /// best figure left.
        /// </summary>
        public static void EnsureManpower(WarRecord war)
        {
            if (war == null) return;
            if (war.AggressorManpowerAtStart <= 0) war.SetManpowerAtStart(war.Aggressor, Manpower(war.Aggressor));
            if (war.DefenderManpowerAtStart <= 0) war.SetManpowerAtStart(war.Defender, Manpower(war.Defender));
        }

        /// <summary>
        /// The breakdown, from <paramref name="kingdom"/>'s side, for the console and the Realm
        /// tab's hint: <c>war score 63.0 = battles 51.0 + prisoners 12.0 (...)</c>.
        /// </summary>
        public static string Describe(WarRecord war, Kingdom kingdom)
        {
            if (war == null || kingdom == null) return "";
            var enemy = war.Other(kingdom);
            var ours = Prisoners(kingdom, enemy);
            var theirs = Prisoners(enemy, kingdom);

            var sb = new StringBuilder();
            sb.Append("war score ").Append(For(war, kingdom).ToString("0.0"))
              .Append(" = battles ").Append(war.BattleScoreFor(kingdom).ToString("0.0"))
              .Append(" + prisoners held ").Append(ours.ToString("0.0"));
            AppendHeld(sb, kingdom, enemy);
            sb.Append(" - prisoners lost ").Append(theirs.ToString("0.0"));
            AppendHeld(sb, enemy, kingdom);
            return sb.ToString();
        }

        private static void AppendHeld(StringBuilder sb, Kingdom holder, Kingdom victim)
        {
            int rulers = 0, leaders = 0, lords = 0;
            foreach (var hero in HeldLords(holder, victim))
            {
                var weight = PrisonerWeight(victim, hero);
                if (weight == DiplomacyConstants.WarScorePrisonerRuler) rulers++;
                else if (weight == DiplomacyConstants.WarScorePrisonerClanLeader) leaders++;
                else lords++;
            }
            if (rulers + leaders + lords == 0) return;

            var parts = new List<string>();
            if (rulers > 0) parts.Add("the ruler");
            if (leaders > 0) parts.Add(leaders + (leaders == 1 ? " clan leader" : " clan leaders"));
            if (lords > 0) parts.Add(lords + (lords == 1 ? " lord" : " lords"));
            sb.Append(" (").Append(string.Join(", ", parts)).Append(')');
        }

        /// <summary>
        /// Sheds a fraction of the battle score each day, with a floor so a small score finishes
        /// clearing. Replaces the flat drift: see <see cref="DiplomacyConstants.WarScoreDecayFractionPerDay"/>.
        /// </summary>
        internal static void Decay(WarRecord war)
        {
            var score = war.BattleScore;
            if (score == 0f) return;
            var magnitude = Math.Abs(score);
            var shed = Math.Max(DiplomacyConstants.WarScoreDecayMinimumPerDay,
                magnitude * DiplomacyConstants.WarScoreDecayFractionPerDay);
            shed = Math.Min(shed, magnitude);
            war.AddBattleScore(score > 0f ? -shed : shed);
        }
    }
}
