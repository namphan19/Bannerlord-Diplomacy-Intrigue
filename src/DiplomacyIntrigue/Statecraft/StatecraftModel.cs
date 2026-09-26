using System;
using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace DiplomacyIntrigue.Statecraft
{
    /// <summary>
    /// Who acts for a realm or a house, and how that hero's skill becomes a number. Every formula
    /// that reads a skill comes through here (design 08 §4), so there is one answer to "whose Charm
    /// is this" and one to "what is 145 Charm worth" - the rule CLAUDE.md §3 calls one resolver
    /// per concept.
    ///
    /// **Derived, never saved** (design 08 §3 rule 8). Actors, pivots and levels are all read from
    /// live hero data on every call. There is deliberately no memo: the pivot is eight realms times
    /// a house's heroes, which is cheap, and a memo is exactly where the day-keyed trap of
    /// CLAUDE.md §1 would come back in (design 08 §4.2, "the memo trap").
    ///
    /// **Off means neutral.** With <see cref="ModSettings.EnableStatecraft"/> off every Level and
    /// Contest is 0, so every term is its neutral value and no XP is granted. That is the A/B
    /// switch S3 runs its control with. The two perks restored in S0 (Firebrand, Silver Tongue)
    /// are not behind it: they are vanilla's own rules the takeover had cut off, not this layer.
    /// </summary>
    public static class StatecraftModel
    {
        public static bool Enabled
        {
            get
            {
                try { return Settings.Current.EnableStatecraft; }
                catch { return false; }
            }
        }

        public static SkillObject SkillOf(Portfolio portfolio)
        {
            switch (portfolio)
            {
                case Portfolio.Ruler: return DefaultSkills.Leadership;
                case Portfolio.Envoy: return DefaultSkills.Charm;
                case Portfolio.Steward: return DefaultSkills.Steward;
                case Portfolio.Treasurer: return DefaultSkills.Trade;
                case Portfolio.Spymaster: return DefaultSkills.Roguery;
                default: return DefaultSkills.Scouting;
            }
        }

        /// <summary>The portfolio a skill is measured in. Each of the six belongs to exactly one.</summary>
        public static Portfolio PortfolioOf(SkillObject skill)
        {
            if (skill == DefaultSkills.Leadership) return Portfolio.Ruler;
            if (skill == DefaultSkills.Charm) return Portfolio.Envoy;
            if (skill == DefaultSkills.Steward) return Portfolio.Steward;
            if (skill == DefaultSkills.Trade) return Portfolio.Treasurer;
            if (skill == DefaultSkills.Roguery) return Portfolio.Spymaster;
            return Portfolio.Watch;
        }

        public static string TitleOf(Portfolio portfolio)
        {
            switch (portfolio)
            {
                case Portfolio.Ruler: return "Ruler";
                case Portfolio.Envoy: return "Envoy";
                case Portfolio.Steward: return "Steward";
                case Portfolio.Treasurer: return "Treasurer";
                case Portfolio.Spymaster: return "Spymaster";
                default: return "Watch";
            }
        }

        // ----- Who acts ----------------------------------------------------------

        /// <summary>
        /// A hero who can act for the house today: alive, grown, free and whole. A captured envoy
        /// hands the table to the next best - taking the enemy's chancellor prisoner is a real
        /// lever, and the Realm tab says so (design 08 §4.1).
        /// </summary>
        public static bool CanAct(Hero hero)
            => hero != null && hero.IsAlive && !hero.IsChild && !hero.IsPrisoner && !hero.IsDisabled;

        /// <summary>
        /// The house's best in a skill, from its lords and its companions. Falls back to the clan's
        /// leader when nobody can act, so a formula always has somebody to read.
        /// </summary>
        public static Hero BestOf(Clan clan, SkillObject skill)
        {
            if (clan == null || skill == null) return null;

            Hero best = null;
            var bestSkill = int.MinValue;
            Consider(clan.AliveLords, skill, ref best, ref bestSkill);
            Consider(clan.Companions, skill, ref best, ref bestSkill);
            return best ?? clan.Leader;
        }

        private static void Consider(IReadOnlyList<Hero> heroes, SkillObject skill, ref Hero best, ref int bestSkill)
        {
            if (heroes == null) return;
            for (var i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                if (!CanAct(hero)) continue;
                var value = hero.GetSkillValue(skill);
                if (value <= bestSkill) continue;
                best = hero;
                bestSkill = value;
            }
        }

        /// <summary>Who holds a portfolio for a realm: its ruler for Leadership, its ruling house's best otherwise.</summary>
        public static Hero Actor(Kingdom kingdom, Portfolio portfolio)
        {
            if (kingdom == null) return null;
            if (portfolio == Portfolio.Ruler) return kingdom.Leader;

            // Design 09 C2: an appointed holder speaks for the seat; an empty seat, or one whose
            // holder cannot act today, falls back to the ruling house's best - the rule before C2.
            return Intrigue.Offices.Speaker(kingdom, portfolio) ?? BestOf(kingdom.RulingClan, SkillOf(portfolio));
        }

        /// <summary>Who holds a portfolio for a house bargaining for itself: its head for Leadership, its best otherwise.</summary>
        public static Hero Actor(Clan clan, Portfolio portfolio)
        {
            if (clan == null) return null;
            return portfolio == Portfolio.Ruler ? clan.Leader : BestOf(clan, SkillOf(portfolio));
        }

        // ----- Level and Contest (design 08 §4.2) -----------------------------------

        /// <summary>
        /// The median of a portfolio's actor across every living realm, read live. A ruler as good
        /// as their peers moves nothing, which is what keeps the AI-only world where seven balance
        /// runs put it, and it absorbs decades of AI skill drift that a fixed pivot would not.
        /// </summary>
        public static float Pivot(SkillObject skill)
        {
            if (skill == null) return StatecraftConstants.FallbackPivot;

            var portfolio = PortfolioOf(skill);
            var values = new List<int>(12);
            foreach (var kingdom in Kingdom.All)
            {
                if (!kingdom.IsRealm()) continue;
                var actor = Actor(kingdom, portfolio);
                if (actor == null) continue;
                values.Add(actor.GetSkillValue(skill));
            }

            if (values.Count == 0) return StatecraftConstants.FallbackPivot;
            values.Sort();
            var mid = values.Count / 2;
            return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2f;
        }

        /// <summary>
        /// A hero against the realms' median, -1..+1. Zero when the layer is off or nobody acts.
        /// </summary>
        public static float Level(Hero hero, SkillObject skill)
        {
            if (!Enabled || hero == null || skill == null) return 0f;
            return Clamp((hero.GetSkillValue(skill) - Pivot(skill)) / StatecraftConstants.LevelSpan, -1f, 1f);
        }

        /// <summary>The Level of whoever holds the portfolio for a realm.</summary>
        public static float Level(Kingdom kingdom, Portfolio portfolio)
            => Level(Actor(kingdom, portfolio), SkillOf(portfolio));

        /// <summary>
        /// Two heroes meeting, -1..+1: positive when the first has the better of it. It compares
        /// Levels rather than raw skill so that a covert contest can pair an attacking skill with a
        /// defending one (Roguery against Scouting) on one scale.
        /// </summary>
        public static float Contest(float levelA, float levelB) => Clamp(levelA - levelB, -1f, 1f);

        // ----- Words -----------------------------------------------------------------

        /// <summary>A hero's name, or "you" for the player's own hero - the way every panel speaks to the player.</summary>
        public static string NameOf(Hero hero)
            => hero == null ? "nobody" : hero == Hero.MainHero ? "you" : hero.Name.ToString();

        public static string SkillName(SkillObject skill) => skill?.Name?.ToString() ?? "?";

        /// <summary>
        /// "Name (Charm 145; the realms' median 225)" - the hero, the skill and the pivot it is
        /// measured against, which is the part a player needs to see why a number moved when
        /// somebody else's did (design 08 §4.2).
        /// </summary>
        public static string Who(Hero hero, SkillObject skill)
        {
            if (hero == null) return "nobody";
            return NameOf(hero) + " (" + SkillName(skill) + " " + hero.GetSkillValue(skill)
                   + "; the realms' median " + Pivot(skill).ToString("0") + ")";
        }

        public static string Signed(float value, string format = "0.0")
            => (value >= 0f ? "+" : "") + value.ToString(format);

        public static string Factor(float value) => "x" + value.ToString("0.00");

        internal static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
