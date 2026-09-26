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

        /// <summary>
        /// Every live grievance a clan holds, heaviest first, for the court ledger UI. An answered
        /// one that weighs nothing is not live (<see cref="AnsweredOf"/> lists those).
        /// </summary>
        public static List<Grievance> Of(ModState state, Clan holder)
        {
            var list = new List<Grievance>();
            if (state == null || holder == null) return list;

            for (var i = 0; i < state.Grievances.Count; i++)
            {
                var g = state.Grievances[i];
                if (g.Holder == holder && g.Weight > 0f) list.Add(g);
            }

            list.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            return list;
        }

        /// <summary>
        /// Wrongs the crown answered that <paramref name="holder"/> still remembers, at no weight:
        /// what the Court tab shows under "answered" (design 09 §4).
        /// </summary>
        public static List<Grievance> AnsweredOf(ModState state, Clan holder, Clan target)
        {
            var list = new List<Grievance>();
            if (state == null || holder == null || target == null) return list;

            for (var i = 0; i < state.Grievances.Count; i++)
            {
                var g = state.Grievances[i];
                if (g.Is(holder, target) && g.Weight <= 0f && IsRemembered(g)) list.Add(g);
            }
            return list;
        }

        /// <summary>
        /// Plain words for the player, not the enum name. Design: "An unjust war", not "UnjustWar 7.8".
        /// Read by the Court tab, the Encyclopedia's ledger under a ReadCourt and the amends
        /// messages, so a slight is named the same wherever it is shown. Here rather than in the UI
        /// since amends (design 09) became a reader in this layer.
        /// </summary>
        public static string TitleOf(GrievanceType type)
        {
            switch (type)
            {
                case GrievanceType.FiefToRival: return "A fief given to another";
                case GrievanceType.UnjustWar: return "An unjust war";
                case GrievanceType.HumiliatingTribute: return "Tribute paid to a foreign crown";
                case GrievanceType.RelativeInCaptivity: return "Kin left in an enemy cell";
                case GrievanceType.FiefLostToEnemy: return "A fief the crown failed to defend";
                case GrievanceType.PolicyAgainstAgenda: return "A policy against their interest";
                case GrievanceType.PeaceWhileWinning: return "Peace made while they were winning";
                case GrievanceType.RequestRefused: return "A request refused";
                case GrievanceType.SuccessionPassedOver: return "Their candidate for the throne passed over";
                // Named as forged. The Court tab is the victim's own court, which was told so when the
                // letters surfaced. On the Encyclopedia only a ReadCourt shows it, and an agent inside
                // the court is placed to know the crown's hand from a forgery - the forger's or a
                // third realm's alike.
                case GrievanceType.ForgedLetters: return "Letters in the crown's hand - forged";
                case GrievanceType.DismissedFromOffice: return "A seat at court taken back";
                default: return "An old slight";
            }
        }

        /// <summary>A grievance the crown answered inside the memory window (design 09 §1).</summary>
        public static bool IsRemembered(Grievance g)
            => g != null && g.WasAnsweredWithin(IntrigueConstants.AmendsMemoryYears);

        /// <summary>
        /// True when <paramref name="target"/> made amends to <paramref name="holder"/> for anything
        /// inside the memory window. The price of another amends doubles on it.
        /// </summary>
        public static bool AnsweredRecently(ModState state, Clan holder, Clan target)
        {
            if (state == null || holder == null || target == null) return false;
            for (var i = 0; i < state.Grievances.Count; i++)
            {
                var g = state.Grievances[i];
                if (g.Is(holder, target) && IsRemembered(g)) return true;
            }
            return false;
        }

        /// <summary>
        /// The weight a renewal arrives at: a wrong repeated after the crown answered it, inside
        /// the memory window, weighs x<see cref="IntrigueConstants.AmendsRepeatWrongFactor"/>. One
        /// place, read by both <see cref="Add"/> and <see cref="WouldAdd"/>.
        /// </summary>
        private static float RenewalWeight(Grievance existing, float value)
            => IsRemembered(existing) ? value * IntrigueConstants.AmendsRepeatWrongFactor : value;

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
                var renewed = RenewalWeight(existing, value);
                return renewed > existing.Weight ? renewed - existing.Weight : 0f;
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

                var repeated = IsRemembered(existing);
                existing.Renew(RenewalWeight(existing, value));
                BlocModel.Invalidate();
                Log.Info("Grievance", holder.Name + " renews " + type + " against " + target.Name
                                      + " at " + existing.Weight.ToString("0.0")
                                      + (repeated ? ", a wrong repeated after amends" : "")
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
            // campaign; a grievance at zero weighs nothing anywhere that reads it. One the crown
            // answered stays at zero until its memory lapses, since a repeat is priced from it.
            state.Grievances.RemoveAll(g => g.IsSpent && !IsRemembered(g));
        }

        /// <summary>
        /// The crown made amends (design 09 C1). The only way a grievance is set to nothing
        /// before it fades; <see cref="Amends.Execute"/> is the only caller, and it has already
        /// checked and paid.
        /// </summary>
        internal static void Answer(Grievance grievance)
        {
            if (grievance == null) return;
            grievance.Answer();
            BlocModel.Invalidate();
        }
    }
}
