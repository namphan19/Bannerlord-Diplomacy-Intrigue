using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Espionage
{
    /// <summary>The terms of a realm's counter-intelligence, so every reader can explain it.</summary>
    public sealed class CounterIntelligenceTerms
    {
        public float Base;

        /// <summary>From the average security of the realm's towns and castles.</summary>
        public float FromSecurity;
        public float AverageSecurity;

        /// <summary>From what the ruler paid at the last weekly upkeep (design 03 §3).</summary>
        public float FromBudget;
        public int WeeklySpent;
        public int WeeklyBudget;

        /// <summary>Before the 0-100 clamp.</summary>
        public float Raw => Base + FromSecurity + FromBudget;

        public float Total
        {
            get
            {
                var raw = Raw;
                return raw < 0f ? 0f : (raw > EspionageConstants.CounterIntelligenceMax ? EspionageConstants.CounterIntelligenceMax : raw);
            }
        }

        public override string ToString()
            => Total.ToString("0.0") + " (base " + Base.ToString("0.0") + ", security "
               + FromSecurity.ToString("+0.0;-0.0;0.0") + " from an average of "
               + AverageSecurity.ToString("0") + ", budget " + FromBudget.ToString("+0.0;-0.0;0.0")
               + " from " + WeeklySpent + " paid last week of " + WeeklyBudget + " ordered)";
    }

    /// <summary>
    /// A realm's defence against spies, 0-100 (design 03 §3). The one resolver: network growth,
    /// mission odds and exposure all read <see cref="Of"/>.
    ///
    /// §3's formula, as the lead settled it on 2026-09-25 (design 03 §9, decisions 9 and 10):
    ///   10 + weekly gold paid / 1500 + 0.05 x average town and castle security, clamped to 0-100.
    /// The "security focus" policy term is gone: vanilla has no such policy, and the policies it
    /// does have that raise security already arrive through the security term, so tying one to a
    /// second term would count it twice.
    ///
    /// **Paid by whoever rules.** A realm has one budget; the ruler pays it weekly from their own
    /// purse. No "is this the player" argument: the player ruler and an AI ruler pay the same way.
    /// Which AI rulers choose to spend, and how much, is 3.6's business; until then an AI realm
    /// orders nothing and defends at its base and its towns.
    /// </summary>
    public static class CounterIntelligence
    {
        public static float Of(ModState state, Kingdom kingdom) => Explain(state, kingdom).Total;

        public static CounterIntelligenceTerms Explain(ModState state, Kingdom kingdom)
        {
            var t = new CounterIntelligenceTerms { Base = EspionageConstants.CounterIntelligenceBase };
            if (kingdom == null) return t;

            var sum = 0f;
            var count = 0;
            foreach (var fief in kingdom.Fiefs)
            {
                if (fief == null) continue;
                sum += fief.Security;
                count++;
            }
            t.AverageSecurity = count == 0 ? 0f : sum / count;
            t.FromSecurity = t.AverageSecurity * EspionageConstants.CounterIntelligencePerSecurity;

            // Money paid, not money promised: the week's spend, recorded by the upkeep.
            var budget = BudgetOf(state, kingdom);
            if (budget != null)
            {
                t.WeeklyBudget = budget.WeeklyBudget;
                t.WeeklySpent = budget.LastWeekSpent;
                t.FromBudget = budget.LastWeekSpent / EspionageConstants.CounterIntelligenceGoldPerPoint;
            }
            return t;
        }

        // ----- The budget ------------------------------------------------------

        public static CounterIntelligenceBudget BudgetOf(ModState state, Kingdom kingdom)
        {
            if (state == null || kingdom == null) return null;
            for (var i = 0; i < state.CounterIntelligenceBudgets.Count; i++)
                if (state.CounterIntelligenceBudgets[i].Kingdom == kingdom) return state.CounterIntelligenceBudgets[i];
            return null;
        }

        /// <summary>
        /// Sets what <paramref name="kingdom"/> means to spend each week. Only a realm has a budget:
        /// an internal war's rising is not one (<see cref="Realms.IsRealm"/>), and neither is a
        /// kingdom with nobody on the throne to pay. Takes effect at the next weekly upkeep - the
        /// defence rises when the money is paid, not when it is ordered.
        /// </summary>
        public static CounterIntelligenceBudget SetBudget(ModState state, Kingdom kingdom, int weekly, out string reason)
        {
            reason = null;
            if (state == null || kingdom == null) { reason = "No realm."; return null; }
            if (!kingdom.IsRealm() || kingdom.Leader == null) { reason = kingdom.Name + " is not a realm with a ruler to pay."; return null; }

            var budget = BudgetOf(state, kingdom);
            if (budget == null)
            {
                budget = new CounterIntelligenceBudget(kingdom);
                state.CounterIntelligenceBudgets.Add(budget);
            }
            budget.SetBudget(weekly);
            Log.Info("Espionage", kingdom.Name + " orders " + budget.WeeklyBudget + " a week for counter-intelligence.");
            return budget;
        }

        // ----- Upkeep ---------------------------------------------------------

        /// <summary>
        /// Once a week, before the networks' own upkeep: each realm's ruler pays its budget, or as
        /// much of it as the purse holds, and the week's defence is what was paid. Run first so a
        /// network growing this week meets the defence paid for this week - the order is fixed by
        /// <see cref="EspionageUpkeep.Weekly"/>, not by the engine's listener order.
        /// </summary>
        public static void WeeklyTick(ModState state)
        {
            if (state == null) return;
            for (var i = state.CounterIntelligenceBudgets.Count - 1; i >= 0; i--)
            {
                var budget = state.CounterIntelligenceBudgets[i];
                try
                {
                    var kingdom = budget.Kingdom;
                    if (kingdom == null || kingdom.IsEliminated || !kingdom.IsRealm())
                    {
                        Log.Info("Espionage", "Dropped the counter-intelligence budget of " + kingdom?.Name + ": no longer a realm.");
                        state.CounterIntelligenceBudgets.RemoveAt(i);
                        continue;
                    }

                    var ruler = kingdom.Leader;
                    var purse = ruler == null ? 0 : ruler.Gold;
                    var spend = Math.Max(0, Math.Min(budget.WeeklyBudget, purse));
                    if (spend > 0) ruler.ChangeHeroGold(-spend);
                    budget.RecordWeek(spend);

                    if (budget.IsEmpty)
                    {
                        state.CounterIntelligenceBudgets.RemoveAt(i);
                        continue;
                    }

                    Log.Info("Espionage", kingdom.Name + "'s counter-intelligence: " + ruler?.Name + " paid " + spend
                                          + " of " + budget.WeeklyBudget + " ordered -> " + Of(state, kingdom).ToString("0.0") + ".");

                    // A player ruler whose purse could not cover the order hears of it: the defence
                    // they are counting on is lower than the one they set.
                    if (spend < budget.WeeklyBudget && ruler != null && ruler == Hero.MainHero)
                        Log.Notify("The treasury paid only " + spend + " of the " + budget.WeeklyBudget
                                   + " denars ordered for hunting foreign agents this week.", Colors.Red);
                }
                catch (Exception ex)
                {
                    Log.Error("Espionage", "The counter-intelligence upkeep of " + budget + " failed.", ex);
                }
            }
        }
    }
}
