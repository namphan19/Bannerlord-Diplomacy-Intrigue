using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// Pulls allies and vassals into a war - and lets them say no.
    ///
    /// Refusal has to be a real option. An alliance that cannot be declined is a suicide
    /// pact, and the AI would rationally never sign one. So refusing an alliance or a
    /// defensive pact costs trust and nothing else: the pact survives and simply lapses at
    /// its next expiry.
    ///
    /// Vassalage is different, and deliberately so. Military service is the substance of
    /// being a vassal, so a vassal that refuses is not exercising an option - it is in open
    /// defiance, and the treaty breaks. That is the moment the hegemony drama of Phase 2
    /// hangs on.
    /// </summary>
    public static class CallToArms
    {
        /// <summary>
        /// Guards against a cascade. When an ally joins, the engine raises WarDeclared
        /// again; without this the war would ripple out through allies of allies until half
        /// of Calradia was involved - which is the exact vanilla failure this mod exists to
        /// remove. Obligations reach one step, not arbitrarily far.
        /// </summary>
        private static bool _issuing;

        /// <summary>
        /// Asks everyone bound to <paramref name="caller"/> whether they will join its war
        /// against <paramref name="enemy"/>. Returns how many answered.
        /// </summary>
        public static int Issue(ModState state, Kingdom caller, Kingdom enemy, bool callerWasAttacked)
        {
            if (_issuing || caller == null || enemy == null || caller == enemy) return 0;

            _issuing = true;
            try
            {
                var answered = 0;
                var obligations = new List<Treaty>();
                foreach (var treaty in TreatyRegistry.AlliesOf(state, caller)) obligations.Add(treaty);

                CapVassalCascade(state, caller, enemy, obligations);

                for (var i = 0; i < obligations.Count; i++)
                {
                    var treaty = obligations[i];
                    var ally = treaty.Other(caller);
                    if (!Applies(state, treaty, caller, ally, enemy, callerWasAttacked)) continue;

                    if (IsPlayerDecision(ally))
                    {
                        AskPlayer(state, treaty, caller, ally, enemy);
                        continue;
                    }

                    if (WouldAnswer(state, treaty, caller, ally, enemy, out var why))
                    {
                        Answer(state, treaty, caller, ally, enemy);
                        answered++;
                    }
                    else
                    {
                        Refuse(state, treaty, caller, ally, why);
                    }
                }
                return answered;
            }
            finally
            {
                _issuing = false;
            }
        }

        /// <summary>
        /// Removes the vassals a patron may not call for this war, keeping those nearest the
        /// target.
        ///
        /// Run 03 is the argument: with nothing on the map but alliances, wars fought for
        /// somebody else were already 30% of the total. A patron with four vassals all owing
        /// offensive service would turn each of its wars into five, and a hegemony would read
        /// as one faction rather than several polities with their own interests.
        ///
        /// Nearest-first is what makes it read correctly - a patron marching west calls the
        /// vassals whose borders face west, not the ones three kingdoms away.
        /// </summary>
        private static void CapVassalCascade(ModState state, Kingdom caller, Kingdom enemy,
            List<Treaty> obligations)
        {
            var vassalages = new List<Treaty>();
            for (var i = 0; i < obligations.Count; i++)
            {
                var treaty = obligations[i];
                if (treaty.Type == TreatyType.Vassalage && treaty.DominantParty == caller)
                    vassalages.Add(treaty);
            }

            var allowed = Hegemony.MaxVassalsToCall(vassalages.Count);
            if (vassalages.Count <= allowed) return;

            vassalages.Sort((x, y) =>
            {
                var dx = AiDiplomacy.Proximity(x.SubordinateParty, enemy);
                var dy = AiDiplomacy.Proximity(y.SubordinateParty, enemy);
                return dy.CompareTo(dx);
            });

            for (var i = allowed; i < vassalages.Count; i++)
            {
                obligations.Remove(vassalages[i]);
                Log.Debug("CallToArms", vassalages[i].SubordinateParty.Name
                                        + " is not called: " + caller.Name + " may raise "
                                        + allowed + " of " + vassalages.Count + " vassals for this war.");
            }
        }

        private static bool Applies(ModState state, Treaty treaty, Kingdom caller, Kingdom ally,
            Kingdom enemy, bool callerWasAttacked)
        {
            if (ally == null || ally == enemy || ally.IsEliminated) return false;
            if (ally.IsAtWarWith(enemy)) return false;

            // A defensive pact never drags anyone into a war of conquest.
            if (treaty.CallToArmsIsDefensiveOnly && !callerWasAttacked) return false;

            // An obligation cannot override a standing agreement with the target: that
            // would force a kingdom to break one treaty to honour another. The pact it
            // already holds with the enemy wins, and the ally is simply not called.
            if (state.HasTreatyForbiddingWar(ally, enemy)) return false;

            // A vassal is called by its patron, not the other way round.
            if (treaty.SubordinatesForeignPolicy && treaty.SubordinateParty != ally) return false;

            return true;
        }

        private static bool IsPlayerDecision(Kingdom ally)
            => ally != null && Hero.MainHero != null && ally.Leader == Hero.MainHero;

        /// <summary>
        /// Whether an AI ally honours the call. Vassals are held to a higher bar because
        /// service is what they agreed to, but even a vassal will not march while its own
        /// realm is collapsing.
        /// </summary>
        public static bool WouldAnswer(ModState state, Treaty treaty, Kingdom caller, Kingdom ally,
            Kingdom enemy, out string why)
        {
            var worstExhaustion = WorstExhaustion(state, ally);
            if (worstExhaustion > DiplomacyConstants.CallToArmsRefuseAboveExhaustion)
            {
                why = "already fighting for its life (exhaustion " + worstExhaustion.ToString("0.0") + ")";
                return false;
            }

            if (treaty.SubordinatesForeignPolicy)
            {
                // A vassal marches while the patron still holds it. Hold is what replaced the
                // old unconditional yes, and the two ways of saying no are deliberately
                // different: being spent is an excuse, resenting the patron is defiance, and
                // only the second earns a mark.
                if (worstExhaustion > DiplomacyConstants.VassalExcusedAboveExhaustion)
                {
                    why = Excused + "spent (exhaustion " + worstExhaustion.ToString("0.0") + ")";
                    return false;
                }

                if (AlreadyServing(state, ally))
                {
                    why = Excused + "already fighting one war for its patron";
                    return false;
                }

                return Hegemony.WouldServe(state, treaty, out why);
            }

            var trust = TrustRegistry.Get(state, ally, caller);
            if (trust < DiplomacyConstants.CallToArmsTrustFloor)
            {
                why = "does not trust " + caller.Name + " (" + trust.ToString("0.0") + ")";
                return false;
            }

            // Joining a war that cannot be won is not loyalty, it is suicide.
            var ourSide = caller.CurrentTotalStrength + ally.CurrentTotalStrength;
            if (enemy.CurrentTotalStrength > ourSide * DiplomacyConstants.CallToArmsHopelessRatio)
            {
                why = enemy.Name + " is too strong for the two of us";
                return false;
            }

            why = null;
            return true;
        }

        public static void Answer(ModState state, Treaty treaty, Kingdom caller, Kingdom ally, Kingdom enemy)
        {
            // The right overload: it stamps the war as caused by a call-to-war agreement,
            // which CasusBelli maps to DefendAlly - the most legitimate reason there is, so
            // honouring a pact never makes a kingdom look like an aggressor.
            DeclareWarAction.ApplyByCallToWarAgreement(ally, enemy);

            // Tag the record the war ledger just opened, so this ally can be released when
            // the caller makes peace. Done after the action because the record is created
            // by the event the action raises.
            var joined = state.OngoingWarBetween(ally, enemy);
            if (joined != null) joined.MarkCalledBy(caller);

            TrustRegistry.OnCallToArmsAnswered(state, caller, ally);

            Log.Info("CallToArms", ally.Name + " answered " + caller.Name
                                   + " and declared war on " + enemy.Name + ".");
            Announce(ally.Name + " honours its " + treaty.Type + " with " + caller.Name
                     + " and joins the war against " + enemy.Name + ".", Colors.Green);
        }

        /// <summary>Marker on a refusal reason: not defiance, just a realm with nothing left.</summary>
        private const string Excused = "excused: ";

        public static void Refuse(ModState state, Treaty treaty, Kingdom caller, Kingdom ally, string why)
        {
            // An excused vassal costs nobody anything. A patron that calls a realm already
            // bleeding out and then punishes it for not coming would be a patron with no
            // vassals, which is not the system this is meant to be.
            if (why != null && why.StartsWith(Excused))
            {
                Log.Info("CallToArms", ally.Name + " could not answer " + caller.Name
                                       + " - " + why.Substring(Excused.Length) + ".");
                return;
            }

            TrustRegistry.OnCallToArmsRefused(state, caller, ally);

            if (treaty.SubordinatesForeignPolicy)
            {
                // Refusal is real defiance now rather than an instant divorce. One mark is a
                // warning the patron can answer - by protecting them, by easing the tribute,
                // or by frightening them - and two inside a year let the bond lapse at its
                // term. Breaking it on the first wobble made every bad month terminal.
                Hegemony.NoteRefusal(state, treaty);

                if (treaty.DefianceMarks < DiplomacyConstants.DefianceMarksToLapse)
                {
                    Announce(ally.Name + " refuses the summons of " + caller.Name + ".", Colors.Yellow);
                    return;
                }

                Log.Info("CallToArms", ally.Name + " defied its patron " + caller.Name
                                       + " once too often - vassalage broken.");
                TreatyRegistry.Break(state, treaty, ally);
                Announce(ally.Name + " renounces its vassalage to " + caller.Name + ".", Colors.Red);
                return;
            }

            Log.Info("CallToArms", ally.Name + " refused " + caller.Name
                                   + (why == null ? "" : " - " + why) + ".");
            Announce(ally.Name + " refuses to honour its " + treaty.Type + " with " + caller.Name + ".",
                Colors.Yellow);
        }

        /// <summary>
        /// Puts the choice to the player when they rule the called kingdom. Letting it
        /// expire counts as a refusal, because silence is an answer.
        /// </summary>
        private static void AskPlayer(ModState state, Treaty treaty, Kingdom caller, Kingdom ally, Kingdom enemy)
        {
            var cost = treaty.SubordinatesForeignPolicy
                ? "Refusing renounces your vassalage and gives " + caller.Name + " a reason for war."
                : "Refusing costs " + (-DiplomacyConstants.TrustCallToArmsRefused).ToString("0")
                  + " trust with " + caller.Name + ", and the " + treaty.Type + " will lapse.";

            var body = caller.Name + " invokes its " + treaty.Type + " and calls you to war against "
                       + enemy.Name + "." + "\n\n" + cost;

            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    "Call to Arms",
                    body,
                    true, true,
                    "Honour it", "Refuse",
                    () => Answer(state, treaty, caller, ally, enemy),
                    () => Refuse(state, treaty, caller, ally, "declined by the ruler"),
                    "", DiplomacyConstants.CallToArmsPlayerResponseHours,
                    () => Refuse(state, treaty, caller, ally, "no answer given"),
                    null, null), true, false);
            }
            catch (System.Exception ex)
            {
                // If the prompt cannot be shown, do not silently commit the player to a war.
                Log.Error("CallToArms", "Could not show the call-to-arms prompt; treating it as a refusal.", ex);
                Refuse(state, treaty, caller, ally, "prompt unavailable");
            }
        }

        /// <summary>
        /// Releases everyone who joined this war only because <paramref name="caller"/>
        /// asked, now that the caller has made peace with <paramref name="enemy"/>.
        ///
        /// The obligation has to run both ways. A vassal called to its patron's war and
        /// then abandoned in it would be paying for a decision it never made, with no way
        /// out - which would make vassalage a trap rather than a bargain.
        /// </summary>
        public static int ReleaseFollowers(ModState state, Kingdom caller, Kingdom enemy)
        {
            if (_releasing || state == null || caller == null || enemy == null) return 0;

            _releasing = true;
            try
            {
                var toRelease = new List<Kingdom>();
                for (var i = 0; i < state.Wars.Count; i++)
                {
                    var war = state.Wars[i];
                    if (!war.IsOngoing || war.CalledBy != caller) continue;

                    var follower = war.Other(enemy);
                    if (follower == null || follower == caller) continue;
                    if (!war.Involves(enemy)) continue;

                    toRelease.Add(follower);
                }

                for (var i = 0; i < toRelease.Count; i++)
                {
                    var follower = toRelease[i];
                    if (!follower.IsAtWarWith(enemy)) continue;

                    // This peace happens *inside* the principal's, because we are handling
                    // the engine's MakePeace event for it. Restoring rather than clearing is
                    // what keeps the outer war from being reported as ended by nobody.
                    var previousCause = Telemetry.NotePeaceCause(Telemetry.PeaceCause.FollowerRelease,
                        "released_by_" + caller.Name.ToString().Replace(' ', '_'));
                    try
                    {
                        MakePeaceAction.Apply(follower, enemy);
                    }
                    finally
                    {
                        Telemetry.RestorePeaceCause(previousCause);
                    }

                    Log.Info("CallToArms", follower.Name + " leaves the war against " + enemy.Name
                                           + " now that " + caller.Name + " has made peace.");
                    Announce(follower.Name + " follows " + caller.Name + " out of the war with "
                             + enemy.Name + ".", Colors.Green);
                }
                return toRelease.Count;
            }
            finally
            {
                _releasing = false;
            }
        }

        /// <summary>Guards the same way <see cref="_issuing"/> does, for the peace side.</summary>
        private static bool _releasing;

        /// <summary>One war for the patron at a time, whatever else is owed.</summary>
        private static bool AlreadyServing(ModState state, Kingdom ally)
        {
            foreach (var war in state.OngoingWarsOf(ally))
                if (war.IsObligationWar) return true;
            return false;
        }

        private static float WorstExhaustion(ModState state, Kingdom kingdom)
        {
            var worst = 0f;
            foreach (var war in state.OngoingWarsOf(kingdom))
            {
                var value = war.ExhaustionOf(kingdom);
                if (value > worst) worst = value;
            }
            return worst;
        }

        private static void Announce(string text, Color color)
        {
            if (!Settings.Current.AnnounceAiDecisions) return;
            Log.Notify(text, color);
        }
    }
}
