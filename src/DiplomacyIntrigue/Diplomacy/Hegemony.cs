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
                // Only wars in which the vassal was attacked, because that is the whole of
                // what a patron is called into (CallToArms.Applies). This used to count every
                // war the vassal was in bar the patron's own summons, so a war the vassal
                // joined for a defensive-pact partner counted as the patron ignoring it -
                // judging the patron against a duty it did not have.
                if (war.Defender != vassal) continue;

                var enemy = war.Aggressor;
                if (enemy == null || enemy == patron) continue;

                if (patron.IsAtWarWith(enemy)) answered++;
                // A patron bound by a treaty to the attacker is never called against it
                // (CallToArms.Applies), so it has not ignored anything. The case that found
                // this: two vassals of the same patron at war with each other, where the
                // vassalage itself forbids the patron joining - and the attacked vassal was
                // charged the full -20 for a protection nobody could have given.
                else if (!state.HasTreatyForbiddingWar(patron, enemy)) ignored++;
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

                // A revolt earlier in this same pass can take siblings out with it, and a link
                // closed a moment ago must not keep drifting, revolting or renewing.
                if (!treaty.IsActive) continue;

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
        ///
        /// The news reaches every other vassal of the same patron, and those already close to
        /// breaking go with it (<see cref="DiplomacyConstants.RevoltJoinBelowHold"/>). Revolt
        /// used to be a decision each vassal took alone, which made a resented hegemony
        /// nearly permanent: every rebel faced the patron plus half its other vassals, lost,
        /// and knelt again. Resentment that is shared has to be able to act together.
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

            var rebels = new List<Treaty> { treaty };
            var watched = 0;
            for (var i = 0; i < siblings.Count; i++)
            {
                var sibling = siblings[i];
                if (sibling == treaty || sibling.SubordinateParty == null) continue;

                sibling.SetHold(HoldOf(sibling) - DiplomacyConstants.SecessionContagionHold);
                if (HoldOf(sibling) < DiplomacyConstants.RevoltJoinBelowHold) rebels.Add(sibling);
                else watched++;
            }

            // Every rebel renounces before anyone declares. The patron's call to arms goes out
            // on the first declaration, and it must reach only the vassals still loyal - not a
            // sibling that is a moment away from joining the other side.
            for (var i = 0; i < rebels.Count; i++)
                Renounce(state, rebels[i]);

            var fighting = new List<string>();
            var refused = new List<string>();
            for (var i = 0; i < rebels.Count; i++)
            {
                var rebel = rebels[i].SubordinateParty;

                TreatyEnforcement.BeginSanctionedWar();
                try
                {
                    DeclareWarAction.ApplyByKingdomDecision(rebel, patron);
                }
                finally
                {
                    TreatyEnforcement.EndSanctionedWar();
                }

                // The renunciation stands either way - the oath is broken and the vassal is
                // free - but the log may only claim the war it can see. Run 04 recorded one of
                // two revolts as a war of independence that never opened.
                (rebel.IsAtWarWith(patron) ? fighting : refused).Add(rebel.Name.ToString());
            }

            var summary = vassal.Name + " renounced " + patron.Name + " after "
                          + days.ToString("0") + " days at breaking point";
            if (rebels.Count > 1)
                summary += ", and " + (rebels.Count - 1) + " other vassal(s) rose with it";
            summary += ". At war for independence: "
                       + (fighting.Count == 0 ? "none" : string.Join(", ", fighting.ToArray()))
                       + (refused.Count == 0
                           ? ""
                           : ". Renounced but the war was refused: " + string.Join(", ", refused.ToArray()))
                       + (watched > 0 ? ". " + watched + " other vassal(s) took note." : ".");
            Log.Info("Hegemony", summary);

            Announce(rebels.Count > 1
                ? vassal.Name + " and " + (rebels.Count - 1) + " other vassal(s) throw off " + patron.Name + "."
                : fighting.Count > 0
                    ? vassal.Name + " throws off " + patron.Name + " and fights for independence."
                    : vassal.Name + " renounces " + patron.Name + ".");
            return true;
        }

        /// <summary>
        /// Breaks a vassal's oath and everything else standing between it and its patron.
        ///
        /// Run 04 found a revolt refused by a DefensivePact the vassal still held with the
        /// very patron it was renouncing: the war veto reads every live treaty, and breaking
        /// the vassalage alone left that one in force. A vassal at breaking point is
        /// repudiating the relationship, not one page of it.
        /// </summary>
        private static void Renounce(ModState state, Treaty vassalage)
        {
            var vassal = vassalage.SubordinateParty;
            var patron = vassalage.DominantParty;
            if (vassal == null || patron == null || !vassalage.IsActive) return;

            TreatyRegistry.Break(state, vassalage, vassal);

            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var other = state.Treaties[i];
                if (other == vassalage || !other.IsActive || !other.ForbidsWar) continue;
                if (!other.IsBetween(vassal, patron)) continue;
                TreatyRegistry.RepudiateAlongside(state, other, vassal);
            }
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

            // Protection is what a vassal buys, and a patron no stronger than the vassal has
            // none to sell. Nothing here used to read the patron's strength at all: threat and
            // pride are the same whoever is asked, so the choice between patrons came down to
            // reach, trust and culture - and a cornered kingdom knelt to its nearest
            // same-culture neighbour even when that neighbour was the weaker of the two.
            var patronStrength = patron.CurrentTotalStrength;
            if (patronStrength <= ownStrength)
            {
                explanation = patron.Name + " is no stronger than " + candidate.Name
                              + " and has no protection to offer => 0.0";
                return 0f;
            }

            // The danger the patron could actually take on. A patron bound by a treaty to one
            // of the candidate's attackers cannot be called against it (CallToArms.Applies),
            // so that share of the threat is not something submitting would solve.
            var threat = 0f;
            var coverable = 0f;
            foreach (var other in Kingdom.All)
            {
                if (other == candidate || other.IsEliminated) continue;
                if (!other.IsAtWarWith(candidate)) continue;

                var strength = other.CurrentTotalStrength;
                threat += strength;
                if (other != patron
                    && (patron.IsAtWarWith(other) || !state.HasTreatyForbiddingWar(patron, other)))
                    coverable += strength;
            }

            // Cover, 0..1: the share of the threat the patron may fight, times how much of it
            // the patron could match on its own. Submitting to a patron that cannot face the
            // enemy trades independence for nothing.
            var cover = threat <= 0f
                ? 0f
                : Clamp(patronStrength / threat, 0f, 1f) * (coverable / threat);

            var threatTerm = Clamp(threat / ownStrength, 0f, 2f) * cover * DiplomacyConstants.SubmissionThreatWeight;
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

            explanation = "threat " + Signed(threatTerm) + " (cover " + cover.ToString("0.00") + ")"
                          + "  reach " + Signed(reachTerm)
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
        ///
        /// The patron's duty starts at signing: it is called at once into the wars its new
        /// vassal is already defending (<see cref="CallToArms.DefendNewVassal"/>).
        /// </summary>
        public static Treaty Submit(ModState state, Kingdom patron, Kingdom vassal, float startingHold,
            int tributePerPeriod, out string reason)
        {
            var treaty = TreatyRegistry.Sign(state, patron, vassal, TreatyType.Vassalage, out reason,
                tributePayer: vassal, tributeAmount: tributePerPeriod);
            if (treaty == null) return null;

            treaty.SetHold(startingHold);
            CallToArms.DefendNewVassal(state, treaty);
            return treaty;
        }

        /// <summary>
        /// Where a vassalage imposed at a peace table starts. Lower when the loser walked out
        /// of a vassalage to this same winner recently, because a vassal brought back by force
        /// is not one that has made its peace with it.
        /// </summary>
        public static float StartingHoldWhenImposed(ModState state, Kingdom winner, Kingdom loser)
        {
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var old = state.Treaties[i];
                if (old.Type != TreatyType.Vassalage || old.Status != TreatyStatus.Broken) continue;
                if (old.BreachedBy != loser || old.SubordinateParty != loser || old.DominantParty != winner) continue;

                var years = (float)(CampaignTime.Now - old.EndedOn).ToYears;
                if (years <= DiplomacyConstants.BrokenTreatyWindowYears)
                    return DiplomacyConstants.HoldAfterFailedRevolt;
            }
            return DiplomacyConstants.HoldOnCoercedSubmission;
        }

        // ================= Spheres =============================================

        /// <summary>
        /// The kingdom at the head of the sphere this one belongs to: its patron if it has
        /// one, otherwise itself. Hegemony is flat, so one step is always enough.
        /// </summary>
        public static Kingdom SphereHead(ModState state, Kingdom kingdom)
            => PatronOf(state, kingdom) ?? kingdom;

        /// <summary>
        /// The head's strength plus every vassal's. Vassals owe their patron troops, so a
        /// sphere is what a rival actually faces - reading the patron's own strength alone
        /// makes a hegemon holding seven kingdoms look like one kingdom.
        /// </summary>
        public static float SphereStrength(ModState state, Kingdom head)
        {
            if (head == null || head.IsEliminated) return 0f;

            var total = head.CurrentTotalStrength;
            foreach (var treaty in state.ActiveTreatiesOf(head))
            {
                if (treaty.Type != TreatyType.Vassalage || treaty.DominantParty != head) continue;
                var vassal = treaty.SubordinateParty;
                if (vassal != null && !vassal.IsEliminated) total += vassal.CurrentTotalStrength;
            }
            return total;
        }

        // ================= Rival hegemons ======================================

        /// <summary>
        /// Courts another patron's neglected vassal. The offer is already a term in
        /// <see cref="HoldTarget"/>, so a rival worth defecting to erodes a link before it
        /// ever succeeds, and courting somebody else's vassal destabilises them even when it
        /// fails.
        ///
        /// **Taking one means war with the patron it was taken from** - the lead's decision
        /// after run 04, where this was the cheapest move on the board and behaved like one:
        /// Battania changed hands four times in two and a half years at valuations that never
        /// moved, because nothing about the act cost anybody anything. A hegemon now reaches
        /// for a rival's client knowing it is reaching for the rival, and the two spheres
        /// follow their patrons in through the ordinary call to arms.
        /// </summary>
        public static bool TryPoach(ModState state, Kingdom suitor)
        {
            if (suitor == null || suitor.IsEliminated) return false;
            if (!IsHegemon(state, suitor)) return false;

            // Poaching is a war decision now, so it clears the same restraint any other war
            // does. A hegemon already fighting a war of its own, or worn out by the last one,
            // does not open a second front by shopping for vassals.
            var canFight = AiDiplomacy.CanTakeOnAnotherWar(state, suitor);

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

                // Already at war with the patron: the war this would start is the war we are
                // already in, so there is nothing further to weigh. Otherwise it has to be a
                // war we are allowed to start and fit to fight - a treaty with the patron
                // forbids taking its vassal too, or this would be the back door around it.
                if (!suitor.IsAtWarWith(patron))
                {
                    if (!canFight) continue;
                    if (!TreatyEnforcement.IsWarAllowed(state, suitor, patron)) continue;
                }

                var value = SubmissionValue(state, vassal, suitor, out _);
                if (value < DiplomacyConstants.AiSubmissionThreshold) continue;
                if (value <= bestValue) continue;

                // Asked before anything is torn up. The old link used to be broken first and
                // the new one signed second, so a signing that failed - on trust, or on a
                // vassal forbidden from treating with outsiders - left the client free and
                // nobody's, with the old patron handed a grievance for a transfer that never
                // happened.
                if (!TreatyRegistry.CanSign(state, suitor, vassal, TreatyType.Vassalage, out _,
                        replacing: treaty)) continue;

                best = treaty;
                bestValue = value;
            }

            if (best == null) return false;

            var client = best.SubordinateParty;
            var oldPatron = best.DominantParty;

            // The old link ends as broken by the client, and charges the client nothing. It was
            // closed with the full breach penalty until the review of run 04, which put the
            // price on both parties to the transfer: -35 and a casus belli from the patron and
            // -12 from every other court on the client, on top of everything the poacher pays.
            // A vassal below Hold 40 has been let down by its patron, and design 04 §5.3 aims
            // the grievance at the kingdom that reached in and took it - which now also means
            // a war. Charging the world's judgment to the client as well priced one act twice,
            // and the trust it cost was exactly what the new signing then checked.
            TreatyRegistry.RepudiateAlongside(state, best, client);

            var moved = Submit(state, suitor, client, DiplomacyConstants.HoldOnVoluntarySubmission,
                DiplomacyConstants.AiDefaultTributePerPeriod, out var reason);
            if (moved == null)
            {
                // CanSign was asked with the old link set aside, so this should not happen.
                // Logged as a warning because it means the two checks have drifted apart.
                Log.Warn("Hegemony", suitor.Name + " courted " + client.Name + " away from "
                                     + oldPatron.Name + " but could not close it: " + reason);
                return false;
            }

            TrustRegistry.Adjust(state, oldPatron, suitor, DiplomacyConstants.PoachingTrustCost,
                "took our vassal " + client.Name);
            ClaimRegistry.GrantBrokenTreatyClaim(state, oldPatron, suitor);

            // Personal as well as institutional: trust is what a realm remembers, relation is
            // what the two rulers think of each other, and this was done to his face.
            var suitorLeader = suitor.Leader;
            var patronLeader = oldPatron.Leader;
            if (suitorLeader != null && patronLeader != null)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                    suitorLeader, patronLeader, -DiplomacyConstants.PoachingRelationLoss, false);
            }

            var warOpened = false;
            var alreadyAtWar = suitor.IsAtWarWith(oldPatron);
            if (!alreadyAtWar)
            {
                TreatyEnforcement.BeginSanctionedWar();
                try
                {
                    DeclareWarAction.ApplyByKingdomDecision(suitor, oldPatron);
                }
                finally
                {
                    TreatyEnforcement.EndSanctionedWar();
                }

                // Read back off the world rather than assumed, for the reason run 04 gave us:
                // a declaration that is refused must never be logged as one that happened.
                warOpened = suitor.IsAtWarWith(oldPatron);
            }

            Log.Info("Hegemony", suitor.Name + " took " + client.Name + " as a vassal from "
                                 + oldPatron.Name + " (submission value " + bestValue.ToString("0") + ")"
                                 + (alreadyAtWar
                                     ? " while already at war with it."
                                     : warOpened
                                         ? " and went to war with " + oldPatron.Name + " over it."
                                         : ", but the war that should have followed was refused."));
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
