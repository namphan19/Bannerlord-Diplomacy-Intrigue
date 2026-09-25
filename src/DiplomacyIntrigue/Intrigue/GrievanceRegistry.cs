using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// The court's memory, and the only place a grievance is created, aged or read.
    ///
    /// One resolver per concept, for the reason recorded in CLAUDE.md §3: legitimacy was
    /// once computed in two places that disagreed, twice, and both times a kingdom honouring
    /// a treaty was punished as an aggressor. Loyalty, blocs, the court UI and - once the
    /// internal-politics work lands (design 07) - the decision to take up arms against the
    /// ruling clan all read their grievances from here and nowhere else.
    ///
    /// Nothing in this file takes an "is this the player" argument, and nothing ever will.
    /// The lead's 2026-09-23 decision (design 02 §9.2) puts the player's own clan under the
    /// same rules as any other; where the player's *experience* needs to differ, that belongs
    /// in the UI layer, not here.
    /// </summary>
    public static class GrievanceRegistry
    {
        // ----- Reading --------------------------------------------------------

        /// <summary>
        /// Everything <paramref name="holder"/> holds against <paramref name="target"/>,
        /// added up. The number loyalty reads.
        /// </summary>
        public static float TotalAgainst(ModState state, Clan holder, Clan target)
        {
            if (state == null || holder == null || target == null || holder == target) return 0f;

            var total = 0f;
            for (var i = 0; i < state.Grievances.Count; i++)
            {
                var g = state.Grievances[i];
                if (g.Is(holder, target)) total += g.Weight;
            }
            return total;
        }

        /// <summary>
        /// What a clan holds against its own crown - the common case, and the one that
        /// decides whether a court fractures. Zero for a clan with no kingdom, and zero for
        /// the ruling clan itself: a clan cannot resent its own leadership from outside, and
        /// a dispute *inside* the ruling clan is a different mechanism (design 07 §1).
        /// </summary>
        public static float AgainstCrown(ModState state, Clan clan)
        {
            var ruling = clan?.Kingdom?.RulingClan;
            if (ruling == null || ruling == clan) return 0f;
            return TotalAgainst(state, clan, ruling);
        }

        /// <summary>Every live grievance a clan holds, heaviest first, for the court ledger UI.</summary>
        public static List<Grievance> Of(ModState state, Clan holder)
        {
            var list = new List<Grievance>();
            if (state == null || holder == null) return list;

            for (var i = 0; i < state.Grievances.Count; i++)
                if (state.Grievances[i].Holder == holder) list.Add(state.Grievances[i]);

            list.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            return list;
        }

        /// <summary>
        /// How much <see cref="Add"/> would raise what <paramref name="holder"/> holds against
        /// <paramref name="target"/>, without adding anything. For a decision that has to weigh
        /// a slight before causing it - a court asked to pay tribute.
        ///
        /// Mirrors the renew rule rather than assuming a fresh record: a clan that already holds
        /// this grievance at full weight gains nothing from a repeat, and one whose record has
        /// decayed gains only the difference. Read from here so the two cannot drift apart.
        /// </summary>
        public static float WouldAdd(ModState state, Clan holder, Clan target, GrievanceType type,
                                     float weight = -1f)
        {
            if (state == null || holder == null || target == null || holder == target) return 0f;
            if (type == GrievanceType.None) return 0f;

            var value = weight >= 0f ? weight : IntrigueConstants.WeightOf(type);
            if (value <= 0f) return 0f;

            for (var i = 0; i < state.Grievances.Count; i++)
            {
                var existing = state.Grievances[i];
                if (existing.Type != type || !existing.Is(holder, target)) continue;
                return value > existing.Weight ? value - existing.Weight : 0f;
            }
            return value;
        }

        // ----- Writing --------------------------------------------------------

        /// <summary>
        /// Records a slight. A repeat of the same type against the same clan **renews** the
        /// existing record rather than adding a second one - the rule claims already use.
        /// Stacking would let a ruler who refuses the same request ten times in a week be
        /// resented ten times as much as one who lost a war, which is not what the court
        /// should mean.
        ///
        /// <paramref name="weight"/> overrides the type's default, for the one source that
        /// needs it: an unjust war scales by (1 - legitimacy), so the same type can arrive
        /// weighing anything from nothing to its ceiling.
        /// </summary>
        public static void Add(ModState state, Clan holder, Clan target, GrievanceType type,
                               float weight = -1f, string reason = null)
        {
            if (state == null || holder == null || target == null || holder == target) return;
            if (type == GrievanceType.None) return;

            var value = weight >= 0f ? weight : IntrigueConstants.WeightOf(type);
            if (value <= 0f) return;   // a fully legitimate war offends nobody

            for (var i = 0; i < state.Grievances.Count; i++)
            {
                var existing = state.Grievances[i];
                if (existing.Type != type || !existing.Is(holder, target)) continue;

                existing.Renew(value);
                BlocModel.Invalidate();
                Log.Info("Grievance", holder.Name + " renews " + type + " against " + target.Name
                                      + " at " + existing.Weight.ToString("0.0")
                                      + (reason == null ? "" : " (" + reason + ")"));
                return;
            }

            state.Grievances.Add(new Grievance(holder, target, type, value));
            BlocModel.Invalidate();
            Log.Info("Grievance", holder.Name + " now holds " + type + " against " + target.Name
                                  + " at " + value.ToString("0.0")
                                  + (reason == null ? "" : " (" + reason + ")"));
        }

        /// <summary>
        /// Wipes what one clan holds against another. For the moments that are supposed to
        /// settle a score rather than add to it - the same reasoning as
        /// <c>Hegemony</c> clearing grievances when an oath is sworn afresh.
        /// </summary>
        public static int Forgive(ModState state, Clan holder, Clan target)
        {
            if (state == null || holder == null || target == null) return 0;

            var removed = state.Grievances.RemoveAll(g => g.Is(holder, target));
            if (removed > 0)
            {
                BlocModel.Invalidate();
                Log.Info("Grievance", holder.Name + " sets aside " + removed
                                      + " grievance(s) against " + target.Name + ".");
            }
            return removed;
        }

        /// <summary>
        /// How fast grievances held against <paramref name="target"/> fade, per day: the base rate
        /// at that house's steward's pace (design 08 S-6). The upkeep and every display read this.
        /// </summary>
        public static float FadePerDay(Clan target)
            => IntrigueConstants.GrievanceDecayPerDay
               * (target == null ? 1f : Statecraft.StatecraftTerms.RecoveryFactor(target));

        // ----- Upkeep ---------------------------------------------------------

        /// <summary>
        /// Ages every grievance and drops the spent ones. Called from the campaign's daily
        /// handler and from <c>diplomacy.tick_days</c>, which drives the *full* daily set -
        /// a diagnostic that runs only part of a tick lies convincingly, and this project has
        /// already lost a day to exactly that (CLAUDE.md §1).
        /// </summary>
        public static void DailyTick(ModState state)
        {
            if (state == null || state.Grievances.Count == 0) return;

            // Design 08 S-6: a grievance fades at the pace of the house it is held against - that
            // house's steward. Read once per house per day, since a court's grievances share one.
            var fade = new Dictionary<Clan, float>();
            for (var i = 0; i < state.Grievances.Count; i++)
            {
                var grievance = state.Grievances[i];
                var target = grievance.Target;
                float rate;
                if (target == null) rate = FadePerDay(null);
                else if (!fade.TryGetValue(target, out rate))
                {
                    rate = FadePerDay(target);
                    fade[target] = rate;
                }
                grievance.Decay(rate);
            }

            // Decay moves every loyalty in the world, so the bloc memo is stale from here.
            BlocModel.Invalidate();

            // Dropping spent records keeps the save from growing without bound across a long
            // campaign; a grievance at zero weighs nothing anywhere that reads it.
            state.Grievances.RemoveAll(g => g.IsSpent);
        }
    }
}
