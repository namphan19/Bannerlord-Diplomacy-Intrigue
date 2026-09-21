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
        /// Hold as it should be read, treating an unset zero as the default. Zero can only mean
        /// a link from a save written before the field: a Hold that has been set never falls
        /// below <see cref="Treaty.MinimumSetHold"/>, so a collapsed link is not mistaken for one.
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
            if (treaty.SubordinateParty == null || treaty.DominantParty == null) return DiplomacyConstants.HoldDefault;

            var t = HoldTermsOf(state, treaty);
            explanation = "base " + DiplomacyConstants.HoldBase.ToString("0")
                          + "  fear " + Signed(t.Fear)
                          + "  protection " + Signed(t.Protection)
                          + "  trust " + Signed(t.Trust)
                          + "  tribute " + Signed(-t.Tribute)
                          + "  wars " + Signed(-t.Wars)
                          + "  rival " + Signed(-t.Rival)
                          + "  culture " + Signed(-t.Culture)
                          + "  dread " + Signed(-t.Dread)
                          + "  => " + t.Target.ToString("0.0");
            return t.Target;
        }

        /// <summary>
        /// Every term of the Hold target, as magnitudes (the negative terms are subtracted).
        /// One computation read by the drift, the diagnostic and the weekly telemetry, so a run
        /// log cannot describe a target the game did not use.
        /// </summary>
        public struct HoldTerms
        {
            public float Fear, Protection, Trust, Tribute, Wars, Rival, Culture, Dread, Target;
        }

        public static HoldTerms HoldTermsOf(ModState state, Treaty treaty)
        {
            var t = new HoldTerms();
            var vassal = treaty?.SubordinateParty;
            var patron = treaty?.DominantParty;
            if (vassal == null || patron == null) { t.Target = DiplomacyConstants.HoldDefault; return t; }

            t.Fear = Power.Balance(patron, vassal) * DiplomacyConstants.HoldStrengthWeight;
            t.Dread = Power.Greed(state, patron) * DiplomacyConstants.HoldDreadWeight;
            t.Protection = Protection(state, treaty, vassal, patron) * DiplomacyConstants.HoldProtectionWeight;
            t.Trust = TrustRegistry.Get(state, vassal, patron) / 100f * DiplomacyConstants.HoldTrustWeight;
            t.Tribute = TributeBurden(treaty, vassal) * DiplomacyConstants.HoldTributeBurdenWeight;
            t.Wars = WarBurden(state, vassal) * DiplomacyConstants.HoldWarBurdenWeight;
            t.Rival = BestRivalPull(state, vassal, patron) * DiplomacyConstants.HoldRivalWeight;
            t.Culture = patron.Culture != vassal.Culture ? DiplomacyConstants.HoldCultureMismatchWeight : 0f;

            t.Target = Clamp(DiplomacyConstants.HoldBase + t.Fear + t.Protection + t.Trust
                             - t.Tribute - t.Wars - t.Rival - t.Culture - t.Dread, 0f, 100f);
            return t;
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
            // moment the link recovers - a bad month is not a rebellion. The line itself moves
            // with the balance of strength, so a vassal that has lost its army stops counting
            // down even at the same Hold.
            if (next < SecessionThreshold(state, treaty))
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
        /// The secession gate: revolt only once Hold has sat below the moving threshold for
        /// <see cref="DiplomacyConstants.SecessionDaysBelowThreshold"/> days. Who pulls the
        /// trigger differs - an AI vassal revolts on its own, a player-ruled one is asked
        /// first - but the revolt itself is <see cref="ExecuteRevolt"/> either way.
        /// </summary>
        private static bool TryRevolt(ModState state, Treaty treaty, Kingdom vassal, Kingdom patron)
        {
            if (treaty.CriticalSince == CampaignTime.Never) return false;

            var days = (float)(CampaignTime.Now - treaty.CriticalSince).ToDays;
            if (days < DiplomacyConstants.SecessionDaysBelowThreshold) return false;

            // A player-ruled vassal is asked rather than taken to war by a timer: seceding
            // is their call to make (design/04 §3.3). The countdown restarts while the
            // question sits open, so declining is a decision to endure rather than a
            // daily nag - and the same duration passes before the question returns.
            if (vassal.Leader == Hero.MainHero)
            {
                AskPlayerToSecede(state, treaty, vassal, patron, days);
                return true;
            }

            ExecuteRevolt(state, treaty, vassal, patron, days);
            return true;
        }

        /// <summary>
        /// Puts the secession to the player whose kingdom is the resentful vassal. Accepting
        /// runs the identical revolt an AI vassal would have run on its own.
        /// </summary>
        private static void AskPlayerToSecede(ModState state, Treaty treaty, Kingdom vassal,
            Kingdom patron, float days)
        {
            // Restart the clock the moment the question is asked - otherwise the daily tick
            // would ask again tomorrow while this inquiry is still open.
            treaty.SetCriticalSince(CampaignTime.Now);

            var hold = HoldOf(treaty);
            var threshold = SecessionThreshold(state, treaty);
            var body = "Our hold on our own realm has sat below the breaking point for "
                       + days.ToString("0") + " days (hold " + hold.ToString("0")
                       + ", secession at " + threshold.ToString("0") + ")."
                       + System.Environment.NewLine + System.Environment.NewLine
                       + "Renouncing breaks our oath to " + patron.Name
                       + " - every court counts it a breach - and opens a war of independence at once."
                       + " Other vassals near their own breaking point may rise with us.";

            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    "Secede from " + patron.Name + "?",
                    body,
                    true, true, "Secede", "Stay",
                    () =>
                    {
                        // Runs from the UI, outside the try below - a throw here would take
                        // the game down with it.
                        try
                        {
                            // Re-asked rather than trusted: while the inquiry sat open the
                            // link may have expired, collapsed or been renewed.
                            if (treaty.IsActive && treaty.SubordinateParty == vassal
                                && treaty.DominantParty == patron)
                            {
                                ExecuteRevolt(state, treaty, vassal, patron, days);
                            }
                            else
                            {
                                Log.Notify("The bond has already changed - the moment has passed.",
                                    Colors.Red);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error("Hegemony", "Seceding failed.", ex);
                        }
                    },
                    () =>
                    {
                        try
                        {
                            Log.Info("Hegemony", "The player chose to stay under " + patron.Name
                                                 + " - for now.");
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error("Hegemony", "Recording the refusal to secede failed.", ex);
                        }
                    }), true);
            }
            catch (System.Exception ex)
            {
                Log.Error("Hegemony", "Could not ask the player to secede.", ex);
            }
        }

        /// <summary>
        /// The player pulling the trigger themselves. A ruler-led vassal already at breaking
        /// point may secede without waiting to be asked: <see cref="IsAtBreakingPoint"/> is
        /// the realm's condition and it is the same one the automatic pull tests - only the
        /// thirty-day clock that paces the AI's revolt is the player's to skip. A vassal not
        /// yet breaking still has the oath-breaking exit, the ordinary Renounce.
        /// </summary>
        internal static void Secede(ModState state, Treaty link)
        {
            var days = link.CriticalSince == CampaignTime.Never
                ? 0f
                : (float)(CampaignTime.Now - link.CriticalSince).ToDays;
            ExecuteRevolt(state, link, link.SubordinateParty, link.DominantParty, days);
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
        private static void ExecuteRevolt(ModState state, Treaty treaty, Kingdom vassal,
            Kingdom patron, float days)
        {
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
                Renounce(state, rebels[i], rebels[i].SubordinateParty);

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

            var rebelNames = new List<string>();
            for (var i = 0; i < rebels.Count; i++) rebelNames.Add(rebels[i].SubordinateParty.Name.ToString());
            Telemetry.Event("revolt", "leader", vassal, "patron", patron,
                "rebels", string.Join("+", rebelNames.ToArray()),
                "atWar", string.Join("+", fighting.ToArray()),
                "refused", refused.Count == 0 ? "none" : string.Join("+", refused.ToArray()),
                "watched", watched, "daysAtBreakingPoint", days,
                "patronStrength", Power.Strength(patron), "patronGreed", Power.Greed(state, patron));

            Announce(rebels.Count > 1
                ? vassal.Name + " and " + (rebels.Count - 1) + " other vassal(s) throw off " + patron.Name + "."
                : fighting.Count > 0
                    ? vassal.Name + " throws off " + patron.Name + " and fights for independence."
                    : vassal.Name + " renounces " + patron.Name + ".");
        }

        /// <summary>
        /// Breaks a vassalage and everything else standing between the two parties, charged to
        /// <paramref name="breaker"/> - the vassal in a revolt, the patron in an annexation or
        /// a defection.
        ///
        /// Run 04 found a revolt refused by a DefensivePact the vassal still held with the
        /// very patron it was renouncing: the war veto reads every live treaty, and breaking
        /// the vassalage alone left that one in force. Whoever ends the bond is repudiating
        /// the relationship, not one page of it.
        /// </summary>
        private static void Renounce(ModState state, Treaty vassalage, Kingdom breaker)
        {
            var vassal = vassalage.SubordinateParty;
            var patron = vassalage.DominantParty;
            if (vassal == null || patron == null || !vassalage.IsActive) return;

            TreatyRegistry.Break(state, vassalage, breaker);

            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var other = state.Treaties[i];
                if (other == vassalage || !other.IsActive || !other.ForbidsWar) continue;
                if (!other.IsBetween(vassal, patron)) continue;
                TreatyRegistry.RepudiateAlongside(state, other, breaker);
            }
        }

        // ================= Annexation ==========================================

        /// <summary>
        /// Whether <paramref name="patron"/> could turn on <paramref name="target"/> to annex
        /// it: the target is its vassal, the patron has grown greedy enough to want provinces
        /// instead (<see cref="DiplomacyConstants.GreedRefusesVassals"/>), and nothing but
        /// treaties between the two stands in the way.
        ///
        /// Annexation happens only through war - the lead's decision. There is no action that
        /// folds a vassal into its patron; a greedy patron tears up the oath and conquers, the
        /// peace table carves the loser up, and the engine destroys a kingdom that loses its
        /// last settlement.
        /// </summary>
        public static bool CouldAnnex(ModState state, Kingdom patron, Kingdom target)
        {
            var link = VassalageOf(state, target);
            if (link == null || link.DominantParty != patron) return false;
            if (AiDiplomacy.WouldTakeVassals(state, patron)) return false;
            return TreatyEnforcement.WhyWarBlocked(state, patron, target) == TreatyEnforcement.Block.TreatyForbidsIt;
        }

        /// <summary>Annexation wars begun this session, for the weekly snapshot.</summary>
        public static int AnnexationWarsThisSession { get; private set; }

        /// <summary>
        /// A greedy patron breaks its vassal's oath in order to conquer it. Charged in full as a
        /// breach - the vassal's trust, every other court's, a BrokenTreaty casus belli handed to
        /// the victim - and every other vassal of the same patron takes the lesson: it loses Hold
        /// as it would watching a revolt, because it has just seen what it is to its patron.
        /// The war itself is declared by the caller.
        /// </summary>
        public static void TurnOnVassal(ModState state, Kingdom patron, Kingdom vassal)
        {
            var link = VassalageOf(state, vassal);
            if (link == null || link.DominantParty != patron) return;

            var siblings = new List<Treaty>();
            CollectVassalages(state, patron, siblings);

            Renounce(state, link, patron);
            AnnexationWarsThisSession++;

            var warned = WarnSiblings(siblings, link);

            Telemetry.Event("annexation_breach", "patron", patron, "vassal", vassal,
                "greed", Power.Greed(state, patron), "siblingsWarned", warned);
            Log.Info("Hegemony", patron.Name + " tore up its vassalage with " + vassal.Name
                                 + " to annex it (greed " + Power.Greed(state, patron).ToString("0.00") + ")"
                                 + (warned > 0 ? "; " + warned + " other vassal(s) saw it happen." : "."));
        }

        /// <summary>
        /// The rest of a patron's vassals watching one of them leave by a door the patron
        /// opened - an annexation or a defection. Each loses the same Hold it would watching a
        /// revolt. Takes the list collected *before* the departure, so the one that left is
        /// still in it and is skipped by reference. Returns how many took the lesson.
        /// </summary>
        /// <summary>
        /// The patron orders peace between a kingdom just sworn to it and any of its other
        /// vassals still at war with that kingdom, and pays for the order in Hold.
        ///
        /// The lead's call (design/04 §12.4.5): the war belonged to the sphere rather than to
        /// that vassal privately, so the patron may end it. Always a white peace - a vassal
        /// cannot be made to hand its winnings to a sibling.
        ///
        /// The sibling loses Hold whether or not it was ahead. Scaling the loss by the war
        /// score was the obvious alternative and lost: it would mean reading the war record at
        /// the exact moment the peace closes it, for a second decimal on a number that already
        /// carries eight terms. What matters is the shape - every vassal a hegemon gains this
        /// way is paid for out of the loyalty it already holds, so a sphere funds its own
        /// expansion and the spiral brakes from the inside instead of needing a cap bolted on.
        /// </summary>
        private static void ReconcileWithSiblings(ModState state, Kingdom patron, Kingdom vassal,
            Treaty signed)
        {
            var siblings = new List<Treaty>();
            CollectVassalages(state, patron, siblings);

            for (var i = 0; i < siblings.Count; i++)
            {
                var sibling = siblings[i];
                if (sibling == signed || !sibling.IsActive) continue;

                var other = sibling.SubordinateParty;
                if (other == null || other.IsEliminated || !other.IsAtWarWith(vassal)) continue;

                var previousCause = Telemetry.NotePeaceCause(
                    Telemetry.PeaceCause.OverlordImposed, "ordered by " + patron.Name);
                try
                {
                    MakePeaceAction.Apply(other, vassal);
                }
                finally
                {
                    Telemetry.RestorePeaceCause(previousCause);
                }

                sibling.SetHold(HoldOf(sibling) - DiplomacyConstants.HoldLostToImposedPeace);

                Telemetry.Event("overlord_imposed_peace", "patron", patron, "aggrieved", other,
                    "newVassal", vassal, "hold", HoldOf(sibling));
                Log.Info("Hegemony", patron.Name + " ordered " + other.Name + " to stop fighting "
                                     + vassal.Name + ", which has just sworn to it. " + other.Name
                                     + " gave up the war with nothing to show, and holds "
                                     + patron.Name + " the worse for it.");
            }
        }

        private static int WarnSiblings(List<Treaty> siblings, Treaty departed)
        {
            var warned = 0;
            for (var i = 0; i < siblings.Count; i++)
            {
                var sibling = siblings[i];
                if (sibling == departed || !sibling.IsActive) continue;
                sibling.SetHold(HoldOf(sibling) - DiplomacyConstants.SecessionContagionHold);
                warned++;
            }
            return warned;
        }

        /// <summary>
        /// A neglected vassal throws itself on its attacker's mercy: the war ends as a
        /// submission - the lead's F3 decision after run 06, where a patron barred from
        /// defending (a truce with the aggressor, the one-step call-to-arms guard) simply
        /// watched its vassal be eaten and paid nothing for it.
        ///
        /// The old bond is closed as broken **by the patron**, not by the vassal. Protection
        /// was the patron's side of the bargain, so failing it is the patron's breach - and
        /// <see cref="TreatyRegistry.Break"/> lands it on the patron's name in every court,
        /// which is what "the lord's image suffers" means in a system with no other
        /// reputation ledger. The patron's remaining vassals take the lesson exactly as
        /// they take a revolt.
        ///
        /// The whole relationship goes, not only the oath (<see cref="Renounce"/>): the first
        /// version broke the vassalage alone and left any pact between the two in force -
        /// the run-04 revolt bug again, and here that pact would also veto the casus belli
        /// the breach has just handed the vassal.
        ///
        /// The caller makes the peace first - signing requires it - then this closes the
        /// old bond and signs the new one through <see cref="Submit"/>, so the new patron is
        /// called into the vassal's other defensive wars the same day, which is the
        /// protection the old one would not give.
        /// </summary>
        public static Treaty Defect(ModState state, Treaty vassalage, Kingdom newPatron, out string reason)
        {
            reason = null;
            var vassal = vassalage?.SubordinateParty;
            var patron = vassalage?.DominantParty;
            if (vassalage == null || !vassalage.IsActive || vassal == null || patron == null)
            {
                reason = "The vassalage it was leaving is no longer in force.";
                return null;
            }

            var siblings = new List<Treaty>();
            CollectVassalages(state, patron, siblings);

            var hold = HoldOf(vassalage);
            Renounce(state, vassalage, patron);
            var warned = WarnSiblings(siblings, vassalage);

            var treaty = Submit(state, newPatron, vassal, DiplomacyConstants.HoldOnDesperateSubmission,
                DiplomacyConstants.AiDefaultTributePerPeriod, out reason,
                route: "defection", detail: "left an undefended patron", settlesWar: true);

            Telemetry.Event("defection", "vassal", vassal, "oldPatron", patron,
                "newPatron", newPatron, "hold", hold, "siblingsWarned", warned,
                "signed", treaty != null);
            return treaty;
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
            Telemetry.Event("vassalage_renewed", "patron", patron, "vassal", vassal, "hold", hold);
            Log.Info("Hegemony", vassal.Name + " renewed its submission to " + patron.Name
                                 + " at hold " + hold.ToString("0.0") + ".");
        }

        /// <summary>
        /// Collapses every vassalage a destroyed kingdom held, either side. The daily tick
        /// would find them tomorrow; doing it on the event means the rest of that day's
        /// upkeep never reads a patron that no longer exists.
        /// </summary>
        public static void OnKingdomDestroyed(ModState state, Kingdom destroyed)
        {
            var links = new List<Treaty>();
            foreach (var treaty in state.ActiveTreatiesOf(destroyed))
                if (treaty.Type == TreatyType.Vassalage) links.Add(treaty);

            for (var i = 0; i < links.Count; i++)
                Collapse(state, links[i], links[i].DominantParty, links[i].SubordinateParty);
        }

        private static void Collapse(ModState state, Treaty treaty, Kingdom patron, Kingdom vassal)
        {
            var siblings = new List<Treaty>();
            CollectVassalages(state, patron, siblings);

            Telemetry.Event("vassalage_collapsed", "patron", patron, "vassal", vassal,
                "patronEliminated", patron == null || patron.IsEliminated,
                "vassalEliminated", vassal == null || vassal.IsEliminated);
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
            Telemetry.Event("defiance_mark", "vassal", treaty.SubordinateParty, "patron", treaty.DominantParty,
                "marks", treaty.DefianceMarks, "hold", HoldOf(treaty));
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
            if (!IsStrongEnoughToHold(patron, candidate))
            {
                explanation = patron.Name + " is no stronger than " + candidate.Name
                              + " and has no protection to offer => 0.0";
                return 0f;
            }

            // The danger the patron could actually take on. A patron bound by a treaty to one
            // of the candidate's attackers cannot be called against it (CallToArms.Applies),
            // so that share of the threat is not something submitting would solve.
            var threat = 0f;
            var fromPatron = 0f;
            var otherThreat = 0f;
            var coverable = 0f;
            foreach (var other in Kingdom.All)
            {
                if (other == candidate || other.IsEliminated) continue;
                if (!other.IsAtWarWith(candidate)) continue;

                var strength = other.CurrentTotalStrength;
                threat += strength;

                // A patron that is itself one of the attackers is a different kind of answer:
                // its war does not get *covered*, it gets *ended* - the submission is the peace
                // (design/04 §12.4.4). Running it through cover would be asking how much of
                // itself the patron could fight, which is why the most surrounded kingdoms used
                // to get the least from kneeling.
                if (other == patron) { fromPatron += strength; continue; }

                otherThreat += strength;
                if (patron.IsAtWarWith(other) || !state.HasTreatyForbiddingWar(patron, other))
                    coverable += strength;
            }

            // Cover, 0..1: of the danger that is not the patron itself, the share it may fight
            // times how much of it it could match. Submitting to a patron that cannot face the
            // enemy trades independence for nothing.
            var cover = otherThreat <= 0f
                ? 0f
                : Clamp(patronStrength / otherThreat, 0f, 1f) * (coverable / otherThreat);

            // What the oath actually relieves: the patron's own war outright, plus the share of
            // the rest it can cover. This reduces to the old cover-only figure exactly whenever
            // the patron is not one of the attackers, so every route that worked before keeps
            // the numbers it was measured with.
            var relief = threat <= 0f ? 0f : (fromPatron + cover * otherThreat) / threat;

            var threatTerm = Clamp(threat / ownStrength, 0f, 2f) * relief * DiplomacyConstants.SubmissionThreatWeight;
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

            // Kneeling to a ruler who wants provinces rather than vassals is volunteering to
            // be annexed. The same dread a sitting vassal feels (Hold), read before signing.
            var dreadTerm = Power.Greed(state, patron) * DiplomacyConstants.HoldDreadWeight;

            // Preferring a bystander to an enemy, priced rather than ranked - see the constant.
            var enemyTerm = fromPatron > 0f ? DiplomacyConstants.SubmissionToEnemyPenalty : 0f;

            var value = threatTerm + reachTerm + wearyTerm + trustTerm
                        - prideTerm - cultureTerm - dreadTerm - enemyTerm;

            explanation = "threat " + Signed(threatTerm) + " (relief " + relief.ToString("0.00") + ")"
                          + "  reach " + Signed(reachTerm)
                          + "  weariness " + Signed(wearyTerm) + "  trust " + Signed(trustTerm)
                          + "  pride " + Signed(-prideTerm) + "  culture " + Signed(-cultureTerm)
                          + "  dread " + Signed(-dreadTerm) + "  enemy " + Signed(-enemyTerm)
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
            int tributePerPeriod, out string reason, string route = "unknown", float value = -1f,
            string detail = null, bool settlesWar = false)
        {
            var treaty = TreatyRegistry.Sign(state, patron, vassal, TreatyType.Vassalage, out reason,
                tributePayer: vassal, tributeAmount: tributePerPeriod, settlesWar: settlesWar);
            if (treaty == null) return null;

            treaty.SetHold(startingHold);

            // Its new siblings are not its enemies. Without this a kingdom that kneels to a
            // hegemon while still at war with one of that hegemon's vassals arrives inside the
            // sphere still fighting it - and DefendNewVassal below would call the patron in
            // against its own vassal.
            ReconcileWithSiblings(state, patron, vassal, treaty);

            Telemetry.Event("vassalage_formed", "patron", patron, "vassal", vassal, "route", route,
                "startHold", startingHold, "value", value, "tribute", tributePerPeriod,
                "patronStrength", Power.Strength(patron), "vassalStrength", Power.Strength(vassal),
                "patronGreed", Power.Greed(state, patron), "terms", detail == null ? "none" : detail.Replace("  ", "|"));

            // The oath is new, so the grievances between the two from the last one are answered.
            var settled = ClaimRegistry.SettleBreaches(state, patron, vassal);
            if (settled > 0)
                Log.Info("Claims", "Submission settled " + settled + " broken-treaty claim(s) between "
                                   + patron.Name + " and " + vassal.Name + ".");

            CallToArms.DefendNewVassal(state, treaty);
            return treaty;
        }

        /// <summary>
        /// Frees the bottom kingdom of every chain of vassalage a save carries - a link whose
        /// patron is itself somebody's vassal. Run once when a session launches.
        ///
        /// Chains were never meant to exist (hegemony is flat, design 04 §1.2) but only half
        /// the rule was enforced until run 06's review: <see cref="TreatyRegistry.CanSign"/>
        /// stopped a hegemon from submitting, not a vassal from taking a vassal, so a defiant
        /// vassal winning at the peace table could build one. The lower link is the one cut -
        /// the lead's call of 2026-09-19 - because it is the one the rule forbids: the middle
        /// kingdom's own oath was legal when it was sworn.
        ///
        /// Dissolved rather than broken: the fault is the mod's missing check, not either
        /// party's, so nobody is charged trust or handed a casus belli. The links to cut are
        /// all chosen before any is cut, so a deeper chain comes apart the same way whatever
        /// order the treaties are stored in.
        /// </summary>
        public static int DissolveChains(ModState state)
        {
            var links = new List<Treaty>();
            CollectLinks(state, links);

            var toCut = new List<Treaty>();
            var overlords = new List<Kingdom>();
            for (var i = 0; i < links.Count; i++)
            {
                var overlord = PatronOf(state, links[i].DominantParty);
                if (overlord == null) continue;
                toCut.Add(links[i]);
                overlords.Add(overlord);
            }

            for (var i = 0; i < toCut.Count; i++)
            {
                var link = toCut[i];
                var vassal = link.SubordinateParty;
                var middle = link.DominantParty;
                var overlord = overlords[i];

                TreatyRegistry.Dissolve(state, link);
                Telemetry.Event("vassal_chain_dissolved", "vassal", vassal, "patron", middle,
                    "overlord", overlord);
                Log.Warn("Hegemony", vassal.Name + " was the vassal of " + middle.Name
                                     + ", itself a vassal of " + overlord.Name
                                     + ". Chains of vassalage are not allowed; " + vassal.Name
                                     + " is released without penalty.");
            }
            return toCut.Count;
        }

        /// <summary>
        /// Settles the broken-treaty claims that an active vassalage has already answered - the
        /// ones acquired before it was signed. Run once when a session launches, for saves made
        /// before <see cref="Submit"/> settled them itself: the run-04 world carries exactly
        /// such a claim, Northern Empire's against Sturgia from a revolt Sturgia knelt again
        /// after, and it let an annexation be filed as a just war.
        /// </summary>
        public static int SettleBreachesPredatingOaths(ModState state)
        {
            var links = new List<Treaty>();
            CollectLinks(state, links);

            var settled = 0;
            for (var i = 0; i < links.Count; i++)
                settled += ClaimRegistry.SettleBreaches(state, links[i].DominantParty, links[i].SubordinateParty,
                    acquiredBefore: links[i].SignedOn);
            return settled;
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
            if (!AiDiplomacy.WouldTakeVassals(state, suitor)) return false;

            Treaty best = null;
            var bestValue = 0f;

            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (!treaty.IsActive || treaty.Type != TreatyType.Vassalage) continue;
                if (treaty.DominantParty == suitor) continue;

                if (!CanPoach(state, suitor, treaty, out var value, out _)) continue;
                if (value <= bestValue) continue;

                best = treaty;
                bestValue = value;
            }

            if (best == null) return false;
            return ExecutePoach(state, suitor, best, bestValue);
        }

        /// <summary>
        /// Whether <paramref name="suitor"/> could take the vassal of <paramref name="link"/>
        /// right now. The per-link gates the weekly scan runs, asked with reasons so the
        /// player's button can say why it is grey.
        ///
        /// Taking one means war with the patron it was taken from - a hegemon reaches for a
        /// rival's client knowing it is reaching for the rival, so a suitor that may not
        /// start that war may not poach either.
        /// </summary>
        public static bool CanPoach(ModState state, Kingdom suitor, Treaty link,
            out float value, out string reason)
        {
            value = 0f;
            reason = null;

            if (suitor == null || suitor.IsEliminated)
            {
                reason = "The courting kingdom no longer exists.";
                return false;
            }
            if (!IsHegemon(state, suitor))
            {
                reason = suitor.Name + " holds no vassals - a realm with none to offer cannot"
                         + " court another's.";
                return false;
            }
            if (!AiDiplomacy.WouldTakeVassals(state, suitor))
            {
                reason = suitor.Name + " wants land, not vassals.";
                return false;
            }
            if (link == null || !link.IsActive || link.Type != TreatyType.Vassalage)
            {
                reason = "They answer to no one.";
                return false;
            }

            var vassal = link.SubordinateParty;
            var patron = link.DominantParty;
            if (vassal == null || patron == null || vassal == suitor || vassal.IsEliminated)
            {
                reason = "The vassal cannot be moved.";
                return false;
            }
            if (patron == suitor)
            {
                reason = "They already answer to us.";
                return false;
            }
            if (suitor.IsAtWarWith(vassal))
            {
                reason = "We are already at war with " + vassal.Name + ".";
                return false;
            }
            if (HoldOf(link) >= DiplomacyConstants.PoachableBelowHold)
            {
                reason = vassal.Name + " is not unhappy enough to listen (hold "
                         + HoldOf(link).ToString("0") + ", needs under "
                         + DiplomacyConstants.PoachableBelowHold.ToString("0") + ").";
                return false;
            }

            // Already at war with the patron: the war this would start is the war we are
            // already in, so there is nothing further to weigh. Otherwise it has to be a
            // war we are allowed to start and fit to fight - a treaty with the patron
            // forbids taking its vassal too, or this would be the back door around it.
            if (!suitor.IsAtWarWith(patron))
            {
                if (!AiDiplomacy.CanTakeOnAnotherWar(state, suitor))
                {
                    reason = "Taking " + vassal.Name + " means war with " + patron.Name
                             + ", and we cannot take on another war.";
                    return false;
                }
                if (!TreatyEnforcement.IsWarAllowed(state, suitor, patron))
                {
                    reason = "Our treaty with " + patron.Name + " forbids the war this would start.";
                    return false;
                }
            }

            value = SubmissionValue(state, vassal, suitor, out var explanation);
            if (value < DiplomacyConstants.AiSubmissionThreshold)
            {
                reason = vassal.Name + " would not kneel to us (valued at " + value.ToString("0")
                         + ", needs " + DiplomacyConstants.AiSubmissionThreshold.ToString("0")
                         + ": " + explanation + ").";
                return false;
            }

            // Asked before anything is torn up. The old link used to be broken first and
            // the new one signed second, so a signing that failed - on trust, or on a
            // vassal forbidden from treating with outsiders - left the client free and
            // nobody's, with the old patron handed a grievance for a transfer that never
            // happened.
            if (!TreatyRegistry.CanSign(state, suitor, vassal, TreatyType.Vassalage, out reason,
                    replacing: link)) return false;

            return true;
        }

        /// <summary>
        /// Executes the poach <see cref="CanPoach"/> cleared: the old bond repudiated at no
        /// charge to the client, the new oath signed, the poacher's price paid in trust,
        /// a casus belli and the patron's enmity - and the war that taking a vassal means.
        /// </summary>
        public static bool ExecutePoach(ModState state, Kingdom suitor, Treaty link, float value)
        {
            var client = link.SubordinateParty;
            var oldPatron = link.DominantParty;

            // The old link ends as broken by the client, and charges the client nothing. It was
            // closed with the full breach penalty until the review of run 04, which put the
            // price on both parties to the transfer: -35 and a casus belli from the patron and
            // -12 from every other court on the client, on top of everything the poacher pays.
            // A vassal below Hold 40 has been let down by its patron, and design 04 §5.3 aims
            // the grievance at the kingdom that reached in and took it - which now also means
            // a war. Charging the world's judgment to the client as well priced one act twice,
            // and the trust it cost was exactly what the new signing then checked.
            TreatyRegistry.RepudiateAlongside(state, link, client);

            var moved = Submit(state, suitor, client, DiplomacyConstants.HoldOnVoluntarySubmission,
                DiplomacyConstants.AiDefaultTributePerPeriod, out var reason, route: "poached", value: value);
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

            Telemetry.Event("poach", "suitor", suitor, "vassal", client, "oldPatron", oldPatron,
                "value", value, "alreadyAtWar", alreadyAtWar, "warOpened", warOpened);
            Log.Info("Hegemony", suitor.Name + " took " + client.Name + " as a vassal from "
                                 + oldPatron.Name + " (submission value " + value.ToString("0") + ")"
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

        // ================= Strength ============================================
        //
        // The hegemony system's questions about strength. The arithmetic lives in Power.cs;
        // these say what it means for a vassalage.

        /// <summary>
        /// Whether a patron could hold this vassal at all: it has to be the stronger of the
        /// two. Every route into vassalage asks this - a voluntary submission, a poach, and a
        /// vassalage imposed at the peace table - because each of them used to ask something
        /// different or nothing, and Northern Empire ended run 04 as patron to six kingdoms
        /// stronger than itself.
        /// </summary>
        public static bool IsStrongEnoughToHold(Kingdom patron, Kingdom vassal)
            => patron != null && vassal != null
               && Power.Strength(patron) > Power.Strength(vassal);

        /// <summary>
        /// The Hold below which this vassal, sustained, revolts. The base line at parity,
        /// raised for a vassal stronger than its patron and lowered for a weaker one (live
        /// strength - whether it could win *now*; see
        /// <see cref="DiplomacyConstants.SecessionCapabilityWeight"/>), and raised again by
        /// dread of a greedy patron (smoothed; <see cref="DiplomacyConstants.SecessionDreadWeight"/>).
        /// Never above the defiance line - a vassal does not skip straight from obedience to war.
        /// </summary>
        public static float SecessionThreshold(ModState state, Treaty vassalage)
        {
            if (vassalage == null) return DiplomacyConstants.HoldSecessionThreshold;

            var line = DiplomacyConstants.HoldSecessionThreshold
                       + DiplomacyConstants.SecessionCapabilityWeight
                       * Power.Balance(vassalage.SubordinateParty, vassalage.DominantParty)
                       + DiplomacyConstants.SecessionDreadWeight
                       * Power.Greed(state, vassalage.DominantParty);
            return Clamp(line, 0f, DiplomacyConstants.HoldDefianceThreshold);
        }

        public static bool IsAtBreakingPoint(ModState state, Treaty vassalage)
            => vassalage != null && HoldOf(vassalage) < SecessionThreshold(state, vassalage);

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
