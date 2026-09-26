using System;
using System.Collections.Generic;
using System.Text;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// A patron sets each vassal's tribute (design 09 C3): the lever design 04 §1.2 promised - "how
    /// much tribute it demands" - that every link had signed at a fixed 500. Hold already weighs
    /// <see cref="Treaty.TributeAmount"/> against what the vassal holds, so the lever is the whole
    /// of the change: more tribute is more income and a lower Hold target, less is the reverse.
    ///
    /// One path for everybody: <see cref="QuoteFor"/> is the preview, <see cref="Set"/> the act, and
    /// the AI's daily choice (<see cref="AiDaily"/>) goes through both. A level holds for
    /// <see cref="DiplomacyConstants.TributeLevelLockDays"/>, for the AI as for the player.
    /// </summary>
    public static class VassalTribute
    {
        /// <summary>The four levels. Not saved: a level is read from the amount on the treaty.</summary>
        public enum Level
        {
            None,
            Light,
            Standard,
            Heavy
        }

        public static readonly Level[] Levels = { Level.None, Level.Light, Level.Standard, Level.Heavy };

        public static int AmountOf(Level level)
        {
            switch (level)
            {
                case Level.Light: return DiplomacyConstants.TributeLight;
                case Level.Standard: return DiplomacyConstants.TributeStandard;
                case Level.Heavy: return DiplomacyConstants.TributeHeavy;
                default: return 0;
            }
        }

        /// <summary>The level an amount is, or null for an amount set some other way (the peace table's own).</summary>
        public static Level? LevelOf(int amount)
        {
            foreach (var level in Levels)
                if (AmountOf(level) == amount) return level;
            return null;
        }

        public static string Describe(int amount)
        {
            var level = LevelOf(amount);
            return level.HasValue ? level.Value + " (" + amount + ")" : amount + " a period";
        }

        // ----- The preview ---------------------------------------------------------------

        public sealed class Quote
        {
            public Treaty Link;
            public Kingdom Patron;
            public Kingdom Vassal;
            public Level Target;
            public int Amount;
            public int CurrentAmount;

            public bool Eligible;
            public string Reason;

            public float Hold;
            public float TargetNow;
            public float TargetAfter;
            public float TermNow;
            public float TermAfter;

            /// <summary>Tribute a game year at this level: twelve periods of seven days.</summary>
            public int YearlyIncome;

            /// <summary>
            /// The target falls under the line below which a vassal withholds its tribute
            /// (<see cref="DiplomacyConstants.HoldPassiveResistanceThreshold"/>): the income is on
            /// paper only. Found live 2026-09-26 - Sturgia, set to Heavy at Hold 40, paid nothing.
            /// </summary>
            public bool Withheld;
        }

        /// <summary>
        /// What setting <paramref name="link"/>'s tribute to <paramref name="level"/> would do, and
        /// whether it can be set today. The Hold figures are <see cref="Hegemony.HoldTermsOf"/> with
        /// only the tribute term changed, so the preview is the target the drift would use.
        /// </summary>
        public static Quote QuoteFor(ModState state, Treaty link, Level level)
        {
            var q = new Quote { Link = link, Target = level, Amount = AmountOf(level) };
            if (state == null || link == null || link.Type != TreatyType.Vassalage || !link.IsActive)
            {
                q.Reason = "no vassalage";
                return q;
            }
            q.Patron = link.DominantParty;
            q.Vassal = link.SubordinateParty;
            q.CurrentAmount = link.TributePayer == q.Vassal ? link.TributeAmount : 0;
            if (q.Patron == null || q.Vassal == null) { q.Reason = "no vassalage"; return q; }

            var terms = Hegemony.HoldTermsOf(state, link);
            q.Hold = Hegemony.HoldOf(link);
            q.TargetNow = terms.Target;
            q.TermNow = terms.Tribute;
            q.TermAfter = Hegemony.TributeBurden(q.Amount, q.Vassal) * DiplomacyConstants.HoldTributeBurdenWeight;
            var raw = terms.Raw + q.TermNow - q.TermAfter;
            q.TargetAfter = raw < 0f ? 0f : (raw > 100f ? 100f : raw);
            q.YearlyIncome = q.Amount * (int)Math.Round(CampaignTime.DaysInYear / (float)DiplomacyConstants.TributePeriodDays);
            q.Withheld = q.Amount > 0 && q.TargetAfter < DiplomacyConstants.HoldPassiveResistanceThreshold;

            if (!Settings.Current.EnableDiplomacy) { q.Reason = "diplomacy is switched off"; return q; }
            if (q.CurrentAmount == q.Amount) { q.Reason = "that is the tribute now"; return q; }
            var sinceSet = (float)(CampaignTime.Now - link.TributeSetOn).ToDays;
            if (sinceSet < DiplomacyConstants.TributeLevelLockDays)
            {
                q.Reason = "set " + sinceSet.ToString("0") + " days ago; it holds for "
                           + (DiplomacyConstants.TributeLevelLockDays - sinceSet).ToString("0") + " more";
                return q;
            }
            q.Eligible = true;
            return q;
        }

        /// <summary>The patron sets the tribute. Checked again here, whoever asked.</summary>
        public static bool Set(ModState state, Treaty link, Level level, out string failed)
        {
            failed = null;
            var q = QuoteFor(state, link, level);
            if (!q.Eligible) { failed = q.Reason; return false; }

            var before = q.CurrentAmount;
            link.SetTributeLevel(q.Vassal, q.Amount, CampaignTime.DaysFromNow(DiplomacyConstants.TributePeriodDays));

            Log.Info("Hegemony", q.Patron.Name + " set " + q.Vassal.Name + "'s tribute " + Describe(before) + " -> "
                                 + Describe(q.Amount) + ": Hold target " + q.TargetNow.ToString("0.0") + " -> "
                                 + q.TargetAfter.ToString("0.0") + " (Hold now " + q.Hold.ToString("0.0") + ").");
            Telemetry.Event("tribute_level", "patron", q.Patron, "vassal", q.Vassal, "from", before, "to", q.Amount,
                "hold", q.Hold, "targetBefore", q.TargetNow, "targetAfter", q.TargetAfter);

            if (q.Patron.Leader == Hero.MainHero)
                Log.Notify(q.Vassal.Name + " will pay " + (q.Amount == 0 ? "no tribute" : q.Amount + " a period")
                           + ". Their Hold now drifts toward " + q.TargetAfter.ToString("0") + ".", Colors.Green);
            else if (q.Vassal.Leader == Hero.MainHero)
                Log.Notify(q.Patron.Name + " now asks " + (q.Amount == 0 ? "no tribute" : q.Amount + " a period")
                           + " of us (was " + before + ").", q.Amount > before ? Colors.Red : Colors.Green);
            return true;
        }

        // ----- The AI (D14) --------------------------------------------------------------

        /// <summary>
        /// The level an AI patron wants for a link: Heavy above a Hold of 70, Light below 40, Standard
        /// between. The same bands for every link; a player patron is shown them as advice, not bound.
        /// </summary>
        public static Level AiLevel(float hold)
            => hold > DiplomacyConstants.AiTributeHeavyAboveHold ? Level.Heavy
                : hold < DiplomacyConstants.AiTributeLightBelowHold ? Level.Light
                : Level.Standard;

        /// <summary>
        /// Every AI patron sets each vassal's tribute to its band when the level has moved out of it
        /// and the lock has passed. Part of the daily hegemony upkeep; the player's links are the
        /// player's to set.
        /// </summary>
        public static void AiDaily(ModState state)
        {
            if (state == null || !Settings.Current.EnableDiplomacy) return;

            var links = new List<Treaty>();
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var t = state.Treaties[i];
                if (t.IsActive && t.Type == TreatyType.Vassalage) links.Add(t);
            }

            foreach (var link in links)
            {
                try
                {
                    var patron = link.DominantParty;
                    if (patron?.Leader == null || patron.Leader == Hero.MainHero) continue;
                    var want = AiLevel(Hegemony.HoldOf(link));
                    if (link.TributeAmount == AmountOf(want) && link.TributePayer == link.SubordinateParty) continue;
                    var q = QuoteFor(state, link, want);
                    if (!q.Eligible) continue;
                    Set(state, link, want, out _);
                }
                catch (Exception ex)
                {
                    Log.Error("Hegemony", "Setting the tribute of " + link + " failed.", ex);
                }
            }
        }

        // ----- Words ---------------------------------------------------------------------

        /// <summary><c>diplomacy.vassal_tribute</c>: every link, its tribute, and every level's effect.</summary>
        public static string DescribeAll(ModState state, Kingdom only = null)
        {
            var sb = new StringBuilder();
            var any = false;
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var link = state.Treaties[i];
                if (!link.IsActive || link.Type != TreatyType.Vassalage) continue;
                if (only != null && link.DominantParty != only && link.SubordinateParty != only) continue;
                any = true;
                var hold = Hegemony.HoldOf(link);
                sb.AppendLine(link.DominantParty?.Name + " <- " + link.SubordinateParty?.Name + ": tribute "
                              + Describe(link.TributePayer == link.SubordinateParty ? link.TributeAmount : 0)
                              + ", Hold " + hold.ToString("0.0") + ", AI band " + AiLevel(hold)
                              + ", last set " + ((float)(CampaignTime.Now - link.TributeSetOn).ToDays > 10000f
                                  ? "never" : ((float)(CampaignTime.Now - link.TributeSetOn).ToDays).ToString("0") + "d ago"));
                foreach (var level in Levels)
                {
                    var q = QuoteFor(state, link, level);
                    sb.AppendLine("    " + level.ToString().PadRight(9) + AmountOf(level).ToString().PadLeft(5)
                                  + "  Hold target " + q.TargetAfter.ToString("0.0") + " (tribute term -" + q.TermAfter.ToString("0.0")
                                  + ")  " + q.YearlyIncome.ToString("N0") + " a year"
                                  + (q.Withheld ? " on paper - withheld under Hold " + DiplomacyConstants.HoldPassiveResistanceThreshold.ToString("0") : "")
                                  + (q.Eligible ? "" : "  - " + q.Reason));
                }
            }
            if (!any) sb.AppendLine(only == null ? "No vassalage anywhere." : "No vassalage involving " + only.Name + ".");
            return sb.ToString().TrimEnd();
        }
    }
}
