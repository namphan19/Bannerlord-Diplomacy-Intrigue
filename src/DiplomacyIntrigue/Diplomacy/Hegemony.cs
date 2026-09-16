using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// One kingdom rising over others: submission, the hold a patron has on a vassal, and the
    /// defiance that eventually undoes it. Design: docs/design/04-hegemony.md.
    ///
    /// **A hegemon is derived, never declared.** Any kingdom holding at least one active
    /// vassalage is one; several coexist by construction; the moment its last vassal leaves it
    /// stops being one, with no event having to fire. That was the project lead's call and it
    /// is the reason this file has no title state, no founding requirements and nothing to
    /// keep in sync - and therefore nothing that can desync.
    ///
    /// The load-bearing number is <see cref="HoldTarget"/>: the lead's source document
    /// (Hegemon-Vassal System.md §16) rendered as a weighted sum of quantities the mod already
    /// tracks. Everything else here is a threshold reading off it.
    /// </summary>
    public static class Hegemony
    {
        // ================= Who holds whom ======================================

        /// <summary>The vassalage that binds this kingdom to a patron, or null if it is free.</summary>
        public static Treaty VassalageOf(ModState state, Kingdom vassal)
        {
            if (vassal == null) return null;
            foreach (var treaty in state.ActiveTreatiesOf(vassal))
                if (treaty.Type == TreatyType.Vassalage && treaty.SubordinateParty == vassal)
                    return treaty;
            return null;
        }

        /// <summary>The kingdom this one answers to under vassalage, or null.</summary>
        public static Kingdom PatronOf(ModState state, Kingdom vassal)
            => VassalageOf(state, vassal)?.DominantParty;

        /// <summary>Every vassalage this kingdom holds as the patron.</summary>
        public static void CollectVassalages(ModState state, Kingdom patron, List<Treaty> into)
        {
            if (patron == null) return;
            foreach (var treaty in state.ActiveTreatiesOf(patron))
                if (treaty.Type == TreatyType.Vassalage && treaty.DominantParty == patron)
                    into.Add(treaty);
        }

        /// <summary>The whole definition: one vassal is enough.</summary>
        public static bool IsHegemon(ModState state, Kingdom kingdom)
        {
            if (kingdom == null) return false;
            foreach (var treaty in state.ActiveTreatiesOf(kingdom))
                if (treaty.Type == TreatyType.Vassalage && treaty.DominantParty == kingdom)
                    return true;
            return false;
        }

        public static int VassalCount(ModState state, Kingdom patron)
        {
            var count = 0;
            foreach (var treaty in state.ActiveTreatiesOf(patron))
                if (treaty.Type == TreatyType.Vassalage && treaty.DominantParty == patron) count++;
            return count;
        }

        /// <summary>
        /// Hold as it should be read, treating an unset zero as the default. Vassalage has
        /// never existed in a campaign (runs 01-03 all report `vassalage=0`), so a zero can
        /// only mean a save written before the field, never a link about to collapse.
        /// </summary>
        public static float HoldOf(Treaty treaty)
        {
            if (treaty == null || treaty.Type != TreatyType.Vassalage) return 0f;
            return treaty.Hold <= 0f ? DiplomacyConstants.HoldDefault : treaty.Hold;
        }

        // ================= What Hold is made of ================================

        /// <summary>
        /// Where Hold is being pulled, given how things stand today. Hold itself moves toward
        /// this by <see cref="DiplomacyConstants.HoldDriftPerDay"/> a day, which is what makes
        /// every term below a pressure rather than an event.
        /// </summary>
        public static float HoldTarget(ModState state, Treaty treaty, out string explanation)
        {
            explanation = null;
            if (treaty == null || treaty.Type != TreatyType.Vassalage) return 0f;

            var vassal = treaty.SubordinateParty;
            var patron = treaty.DominantParty;
            if (vassal == null || patron == null) return DiplomacyConstants.HoldDefault;

            var fear = Clamp(Ratio(patron.CurrentTotalStrength, vassal.CurrentTotalStrength) - 1f, -1f, 1f)
                       * DiplomacyConstants.HoldStrengthWeight;
            var protection = Protection(state, treaty, vassal, patron) * DiplomacyConstants.HoldProtectionWeight;
            var trust = TrustRegistry.Get(state, vassal, patron) / 100f * DiplomacyConstants.HoldTrustWeight;
            var tribute = TributeBurden(treaty, vassal) * DiplomacyConstants.HoldTributeBurdenWeight;
            var wars = WarBurden(state, vassal) * DiplomacyConstants.HoldWarBurdenWeight;
            var rival = BestRivalPull(state, vassal, patron) * DiplomacyConstants.HoldRivalWeight;
            var culture = patron.Culture != vassal.Culture ? DiplomacyConstants.HoldCultureMismatchWeight : 0f;

            var target = DiplomacyConstants.HoldBase + fear + protection + trust
                         - tribute - wars - rival - culture;

            explanation = "base " + DiplomacyConstants.HoldBase.ToString("0")
                          + "  fear " + Signed(fear)
                          + "  protection " + Signed(protection)
                          + "  trust " + Signed(trust)
                          + "  tribute " + Signed(-tribute)
                          + "  wars " + Signed(-wars)
                          + "  rival " + Signed(-rival)
                          + "  culture " + Signed(-culture)
                          + "  => " + Clamp(target, 0f, 100f).ToString("0.0");

            return Clamp(target, 0f, 100f);
        }

        /// <summary>
        /// Whether the patron is actually protecting this vassal, from -1 to +1.
        ///
        /// Measured continuously from the current wars rather than as a one-off penalty when a
        /// call goes unanswered, which is a deliberate departure from the spec's discrete
        /// -25: a patron that ignores a vassal's war for three weeks loses three weeks of
        /// Hold, and one that belatedly joins starts earning it back the same day. No event
        /// bookkeeping, no saved flags, and it cannot get stuck.
        /// </summary>
        private static float Protection(ModState state, Treaty treaty, Kingdom vassal, Kingdom patron)
        {
            var answered = 0;
            var ignored = 0;

            foreach (var war in state.OngoingWarsOf(vassal))
            {
                var enemy = war.Other(vassal);
                if (enemy == null || enemy == patron) continue;

                // Only wars the vassal did not pick for someone else. A war it was called
                // into by this very patron is a burden, counted separately.
                if (war.CalledBy == patron) continue;

                if (patron.IsAtWarWith(enemy)) answered++;
                else ignored++;
            }

            if (answered + ignored == 0) return 0f;
            return (answered - ignored) / (float)(answered + ignored);
        }

        /// <summary>
        /// Tribute weighed against what the vassal holds, not in absolute denars: 500 a period
        /// is ruinous for two castles and trivial for a dozen towns. Un-tuned.
        /// </summary>
        private static float TributeBurden(Treaty treaty, Kingdom vassal)
        {
            if (treaty.TributeAmount <= 0 || treaty.TributePayer != vassal) return 0f;

            var fiefs = 0;
            var settlements = vassal.Settlements;
            for (var i = 0; i < settlements.Count; i++)
                if (settlements[i].IsFortification) fiefs++;
            if (fiefs <= 0) return 1f;

            var tolerable = fiefs * DiplomacyConstants.HoldTributePerFiefForFullBurden;
            return Clamp(treaty.TributeAmount / tolerable, 0f, 1f);
        }

        /// <summary>Wars the vassal is fighting because somebody else asked it to.</summary>
        private static float WarBurden(ModState state, Kingdom vassal)
        {
            var obligations = 0;
            foreach (var war in state.OngoingWarsOf(vassal))
                if (war.IsObligationWar) obligations++;

            return Clamp(obligations / (float)DiplomacyConstants.HoldWarBurdenSaturation, 0f, 1f);
        }

        /// <summary>
        /// Somebody stronger than our patron, who already holds vassals and is not at war with
        /// us, is a standing invitation. The source document's §8.6 - obedience often rests on
        /// having no alternative.
        /// </summary>
        private static float BestRivalPull(ModState state, Kingdom vassal, Kingdom patron)
        {
            var best = 0f;
            foreach (var other in Kingdom.All)
            {
                if (other == patron || other == vassal || other.IsEliminated) continue;
                if (other.IsAtWarWith(vassal)) continue;
                if (!IsHegemon(state, other)) continue;

                var pull = Clamp(Ratio(other.CurrentTotalStrength, patron.CurrentTotalStrength) - 1f, 0f, 1f);
                if (pull > best) best = pull;
            }
            return best;
        }

        // ================= Daily life of a hegemony ============================

        /// <summary>
        /// Drifts every link, then acts on what the drift produced: renewal, lapse, revolt,
        /// and the collapse of a hegemon that no longer exists.
        ///
        /// Runs before the treaty expiry sweep, because a vassal with high Hold renewing its
        /// term has to happen before the registry retires the treaty for running out.
        /// </summary>
        public static void DailyTick(ModState state)
        {
            var links = new List<Treaty>();
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (treaty.IsActive && treaty.Type == TreatyType.Vassalage) links.Add(treaty);
            }

            for (var i = 0; i < links.Count; i++)
            {
                var treaty = links[i];
                var vassal = treaty.SubordinateParty;
                var patron = treaty.DominantParty;

                if (patron == null || vassal == null) continue;

                // A patron that no longer exists holds nobody. Dissolved, not broken: nobody
                // chose this, so nobody should pay for it in trust.
                if (patron.IsEliminated || vassal.IsEliminated)
                {
                    Collapse(state, treaty, patron, vassal);
                    continue;
                }

                Drift(state, treaty);
                ForgetOldDefiance(treaty);

                if (TryRevolt(state, treaty, vassal, patron)) continue;
                TryRenew(state, treaty, vassal, patron);
            }
        }

        private static void Drift(ModState state, Treaty treaty)
        {
            var current = HoldOf(treaty);
            var target = HoldTarget(state, treaty, out _);

            var step = DiplomacyConstants.HoldDriftPerDay;
            var next = current < target
                ? (current + step > target ? target : current + step)
                : (current - step < target ? target : current - step);

            treaty.SetHold(next);

            // The revolt clock runs only while the collapse is sustained, and resets the
            // moment the link recovers - a bad month is not a rebellion.
            if (next < DiplomacyConstants.HoldSecessionThreshold)
            {
                if (treaty.CriticalSince == CampaignTime.Never) treaty.SetCriticalSince(CampaignTime.Now);
            }
            else if (treaty.CriticalSince != CampaignTime.Never)
            {
                treaty.SetCriticalSince(CampaignTime.Never);
            }
        }

        private static void ForgetOldDefiance(Treaty treaty)
        {
            if (treaty.DefianceMarks <= 0) return;
            if (treaty.LastDefianceOn == CampaignTime.Never) return;

            var days = (float)(CampaignTime.Now - treaty.LastDefianceOn).ToDays;
            if (days > DiplomacyConstants.DefianceMarkMemoryDays) treaty.ForgiveDefiance();
        }

        /// <summary>
        /// Independence, once the collapse has held for long enough. The vassal breaks its
        /// oath and declares war: a revolt is a breach, and the world judges it as one.
        /// </summary>
        private static bool TryRevolt(ModState state, Treaty treaty, Kingdom vassal, Kingdom patron)
        {
            if (treaty.CriticalSince == CampaignTime.Never) return false;

            var days = (float)(CampaignTime.Now - treaty.CriticalSince).ToDays;
            if (days < DiplomacyConstants.SecessionDaysBelowThreshold) return false;

            // Other vassals are watching. The demonstration effect starts when somebody
            // defies, not when they win - which is a departure from the spec, where it was
            // conditional on victory. Detecting victory means hooking the peace that ends a
            // war we have already broken the treaty for, and the moment of defiance is the
            // moment the others learn it is possible.
            var siblings = new List<Treaty>();
            CollectVassalages(state, patron, siblings);

            TreatyRegistry.Break(state, treaty, vassal);

            TreatyEnforcement.BeginSanctionedWar();
            try
            {
                DeclareWarAction.ApplyByKingdomDecision(vassal, patron);
            }
            finally
            {
                TreatyEnforcement.EndSanctionedWar();
            }

            var spread = 0;
            for (var i = 0; i < siblings.Count; i++)
            {
                var sibling = siblings[i];
                if (sibling == treaty) continue;
                sibling.SetHold(HoldOf(sibling) - DiplomacyConstants.SecessionContagionHold);
                spread++;
            }

            Log.Info("Hegemony", vassal.Name + " renounced " + patron.Name
                                 + " and declared war for its independence after "
                                 + days.ToString("0") + " days at breaking point"
                                 + (spread > 0 ? "; " + spread + " other vassal(s) took note." : "."));
            Announce(vassal.Name + " throws off " + patron.Name + " and fights for independence.");
            return true;
        }

        /// <summary>
        /// At the end of its term a vassal decides rather than simply lapsing. High Hold and
        /// no standing defiance renews it in place; anything else is left to the registry's
        /// expiry sweep, which ends it as honoured.
        /// </summary>
        private static void TryRenew(ModState state, Treaty treaty, Kingdom vassal, Kingdom patron)
        {
            if (!treaty.HasRunOut) return;

            var hold = HoldOf(treaty);
            if (hold < DiplomacyConstants.HoldRenewThreshold) return;
            if (treaty.DefianceMarks >= DiplomacyConstants.DefianceMarksToLapse) return;

            treaty.ExtendTo(CampaignTime.YearsFromNow(DiplomacyConstants.VassalageYears));
            Log.Info("Hegemony", vassal.Name + " renewed its submission to " + patron.Name
                                 + " at hold " + hold.ToString("0.0") + ".");
        }

        private static void Collapse(ModState state, Treaty treaty, Kingdom patron, Kingdom vassal)
        {
            var siblings = new List<Treaty>();
            CollectVassalages(state, patron, siblings);

            TreatyRegistry.Dissolve(state, treaty);
            Log.Info("Hegemony", "Vassalage dissolved: " + (patron?.Name.ToString() ?? "a destroyed patron")
                                 + " no longer holds " + (vassal?.Name.ToString() ?? "a destroyed vassal") + ".");

            // A grace period between the freed, so a collapse does not instantly become a
            // free-for-all of simultaneous wars between kingdoms that were just allies by
            // force. Refusals are ignored: some of them will already be at war.
            for (var i = 0; i < siblings.Count; i++)
            {
                var other = siblings[i].SubordinateParty;
                if (other == null || other == vassal || other.IsEliminated) continue;
                if (vassal == null || vassal.IsEliminated) continue;

                TreatyRegistry.Sign(state, vassal, other, TreatyType.NonAggressionPact, out _);
            }
        }

        // ================= Obligations and defiance ============================

        /// <summary>
        /// Whether this vassal will still answer its patron's call. Below the resistance
        /// threshold it will not, and refusing earns a mark.
        /// </summary>
        public static bool WouldServe(ModState state, Treaty treaty, out string why)
        {
            why = null;
            var hold = HoldOf(treaty);

            if (hold < DiplomacyConstants.HoldPassiveResistanceThreshold)
            {
                why = "resents " + (treaty.DominantParty?.Name.ToString() ?? "its patron")
                      + " too much to march (hold " + hold.ToString("0") + ")";
                return false;
            }

            return true;
        }

        /// <summary>Records a refusal, and lets the link lapse once defiance is a habit.</summary>
        public static void NoteRefusal(ModState state, Treaty treaty)
        {
            if (treaty == null || treaty.Type != TreatyType.Vassalage) return;

            treaty.AddDefianceMark();
            var vassal = treaty.SubordinateParty;
            var patron = treaty.DominantParty;

            Log.Info("Hegemony", (vassal?.Name.ToString() ?? "A vassal") + " defied "
                                 + (patron?.Name.ToString() ?? "its patron") + " (mark "
                                 + treaty.DefianceMarks + " of " + DiplomacyConstants.DefianceMarksToLapse
                                 + ", hold " + HoldOf(treaty).ToString("0.0") + ").");
        }

        /// <summary>
        /// Whether a vassal is defiant enough to treat with outsiders despite owing its
        /// foreign policy. This is what makes the tier mean something: below the threshold the
        /// prohibition simply stops being enforced, and signing carries the cost.
        /// </summary>
        public static bool WillDefyForeignPolicy(Treaty vassalage)
            => vassalage != null && HoldOf(vassalage) < DiplomacyConstants.HoldDefianceThreshold;

        /// <summary>
        /// How many vassals a patron may call into one war: half, rounded up, nearest the
        /// target first.
        ///
        /// Run 03 is the argument for a cap at all. With nothing but alliances on the map,
        /// wars honouring somebody else's quarrel were already 30% of all wars; a patron with
        /// four vassals owing offensive service would turn each of its wars into five.
        /// </summary>
        public static int MaxVassalsToCall(int vassalCount)
            => vassalCount <= 1 ? vassalCount : (vassalCount + 1) / 2;

        // ================= Becoming a vassal ===================================

        /// <summary>
        /// What submitting to this patron is worth to a cornered kingdom, on the same scale as
        /// the treaty valuations - and computed by the same function whoever is asking, so the
        /// number a player is shown is the number the AI used.
        /// </summary>
        public static float SubmissionValue(ModState state, Kingdom candidate, Kingdom patron,
            out string explanation)
        {
            explanation = null;
            if (candidate == null || patron == null || candidate == patron) return 0f;

            var ownStrength = candidate.CurrentTotalStrength;
            if (ownStrength <= 0f) return 0f;

            var threat = 0f;
            foreach (var other in Kingdom.All)
            {
                if (other == candidate || other.IsEliminated) continue;
                if (!other.IsAtWarWith(candidate)) continue;
                threat += other.CurrentTotalStrength;
            }

            var threatTerm = Clamp(threat / ownStrength, 0f, 2f) * DiplomacyConstants.SubmissionThreatWeight;
            var reachTerm = AiDiplomacy.Proximity(patron, candidate) * DiplomacyConstants.SubmissionReachWeight;
            var wearyTerm = state.WearinessOf(candidate) / 100f * DiplomacyConstants.SubmissionWearinessWeight;
            var trustTerm = TrustRegistry.Get(state, candidate, patron) / 100f * DiplomacyConstants.SubmissionTrustWeight;

            var strongestNeighbour = 1f;
            foreach (var other in Kingdom.All)
            {
                if (other == candidate || other.IsEliminated) continue;
                if (other.CurrentTotalStrength > strongestNeighbour) strongestNeighbour = other.CurrentTotalStrength;
            }
            var prideTerm = Clamp(ownStrength / strongestNeighbour, 0f, 1f) * DiplomacyConstants.SubmissionPrideWeight;
            var cultureTerm = candidate.Culture != patron.Culture ? DiplomacyConstants.SubmissionCultureWeight : 0f;

            var value = threatTerm + reachTerm + wearyTerm + trustTerm - prideTerm - cultureTerm;

            explanation = "threat " + Signed(threatTerm) + "  reach " + Signed(reachTerm)
                          + "  weariness " + Signed(wearyTerm) + "  trust " + Signed(trustTerm)
                          + "  pride " + Signed(-prideTerm) + "  culture " + Signed(-cultureTerm)
                          + "  => " + value.ToString("0.0")
                          + " (submits at " + DiplomacyConstants.AiSubmissionThreshold.ToString("0") + ")";

            return value;
        }

        /// <summary>
        /// Signs a vassalage. One function for every route in - imposed at a peace table,
        /// offered voluntarily, or transferred by poaching - so the starting Hold is the only
        /// thing that differs between them.
        /// </summary>
        public static Treaty Submit(ModState state, Kingdom patron, Kingdom vassal, float startingHold,
            int tributePerPeriod, out string reason)
        {
            var treaty = TreatyRegistry.Sign(state, patron, vassal, TreatyType.Vassalage, out reason,
                tributePayer: vassal, tributeAmount: tributePerPeriod);
            if (treaty == null) return null;

            treaty.SetHold(startingHold);
            return treaty;
        }

        // ================= Rival hegemons ======================================

        /// <summary>
        /// Courts another patron's neglected vassal. The cheapest interesting thing in the
        /// whole design: the offer is already a term in <see cref="HoldTarget"/>, so a rival
        /// worth defecting to erodes a link before it ever succeeds, and courting somebody
        /// else's vassal destabilises them even when it fails.
        /// </summary>
        public static bool TryPoach(ModState state, Kingdom suitor)
        {
            if (suitor == null || suitor.IsEliminated) return false;
            if (!IsHegemon(state, suitor)) return false;

            Treaty best = null;
            var bestValue = 0f;

            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (!treaty.IsActive || treaty.Type != TreatyType.Vassalage) continue;
                if (treaty.DominantParty == suitor) continue;

                var vassal = treaty.SubordinateParty;
                var patron = treaty.DominantParty;
                if (vassal == null || patron == null || vassal == suitor) continue;
                if (vassal.IsEliminated || suitor.IsAtWarWith(vassal)) continue;
                if (HoldOf(treaty) >= DiplomacyConstants.PoachableBelowHold) continue;

                var value = SubmissionValue(state, vassal, suitor, out _);
                if (value < DiplomacyConstants.AiSubmissionThreshold) continue;
                if (value <= bestValue) continue;

                best = treaty;
                bestValue = value;
            }

            if (best == null) return false;

            var client = best.SubordinateParty;
            var oldPatron = best.DominantParty;

            // The old link ends as broken by the client: it walked away from its oath, and
            // the patron is owed the grievance that follows.
            TreatyRegistry.Break(state, best, client);

            var moved = Submit(state, suitor, client, DiplomacyConstants.HoldOnVoluntarySubmission,
                DiplomacyConstants.AiDefaultTributePerPeriod, out var reason);
            if (moved == null)
            {
                Log.Info("Hegemony", suitor.Name + " courted " + client.Name + " away from "
                                     + oldPatron.Name + " but could not close it: " + reason);
                return false;
            }

            TrustRegistry.Adjust(state, oldPatron, suitor, DiplomacyConstants.PoachingTrustCost,
                "took our vassal " + client.Name);
            ClaimRegistry.GrantBrokenTreatyClaim(state, oldPatron, suitor);

            Log.Info("Hegemony", suitor.Name + " took " + client.Name + " as a vassal from "
                                 + oldPatron.Name + " (submission value " + bestValue.ToString("0") + ").");
            Announce(client.Name + " transfers its allegiance from " + oldPatron.Name + " to " + suitor.Name + ".");
            return true;
        }

        // ================= Reporting ===========================================

        public static int CountHegemons(ModState state)
        {
            var count = 0;
            foreach (var kingdom in Kingdom.All)
                if (!kingdom.IsEliminated && IsHegemon(state, kingdom)) count++;
            return count;
        }

        public static void CollectLinks(ModState state, List<Treaty> into)
        {
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (treaty.IsActive && treaty.Type == TreatyType.Vassalage) into.Add(treaty);
            }
        }

        // ================= Small helpers =======================================

        private static float Ratio(float a, float b) => b <= 0f ? 1f : a / b;

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        private static string Signed(float v) => (v >= 0f ? "+" : "") + v.ToString("0.0");

        private static void Announce(string text)
        {
            if (!Settings.Current.AnnounceAiDecisions) return;
            Log.Notify(text, Colors.Magenta);
        }
    }
}
