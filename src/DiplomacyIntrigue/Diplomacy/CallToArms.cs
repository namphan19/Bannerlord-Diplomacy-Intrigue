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
    /// Vassalage is different, and deliberately so, and it runs both ways. Military service
    /// is the substance of being a vassal, so a vassal that refuses is not exercising an
    /// option - it is in open defiance, it earns a mark, and the second mark breaks the
    /// treaty. Protection is the substance of being a patron, so a patron is called when its
    /// vassal is attacked; refusing costs it the vassal's trust and, day by day, its Hold.
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
                    if (!Applies(state, treaty, caller, ally, enemy, callerWasAttacked, out var boundChoice)) continue;
                    if (Put(state, treaty, caller, ally, enemy, callerWasAttacked, boundChoice)) answered++;
                }
                return answered;
            }
            finally
            {
                _issuing = false;
            }
        }

        /// <summary>
        /// Calls a new patron into the wars its vassal was already defending when it
        /// submitted. Returns how many it joined.
        ///
        /// The call to arms fires when a war is declared, so a kingdom that knelt *because* it
        /// was under attack would otherwise get a patron that owes it nothing for the very
        /// wars that made it kneel - and whose failure to join them then counts against Hold
        /// from the first day. Wars the vassal started itself are not the patron's to answer,
        /// exactly as with a war declared later.
        /// </summary>
        public static int DefendNewVassal(ModState state, Treaty vassalage)
        {
            if (_issuing || state == null || vassalage == null) return 0;
            if (!vassalage.IsActive || vassalage.Type != TreatyType.Vassalage) return 0;

            var vassal = vassalage.SubordinateParty;
            var patron = vassalage.DominantParty;
            if (vassal == null || patron == null) return 0;

            // Collected first: answering opens war records, and the ledger is what we read.
            var attackers = new List<Kingdom>();
            foreach (var war in state.OngoingWarsOf(vassal))
                if (war.Defender == vassal && war.Aggressor != null && war.Aggressor != patron)
                    attackers.Add(war.Aggressor);

            _issuing = true;
            try
            {
                var answered = 0;
                for (var i = 0; i < attackers.Count; i++)
                {
                    if (!Applies(state, vassalage, vassal, patron, attackers[i], callerWasAttacked: true, out var boundChoice)) continue;
                    if (Put(state, vassalage, vassal, patron, attackers[i], callerWasAttacked: true, boundChoice)) answered++;
                }
                return answered;
            }
            finally
            {
                _issuing = false;
            }
        }

        /// <summary>
        /// Puts one obligation to the party that owes it: the player is asked, an AI decides.
        /// Returns true only when an AI answered on the spot.
        /// </summary>
        private static bool Put(ModState state, Treaty treaty, Kingdom caller, Kingdom ally, Kingdom enemy,
            bool callerWasAttacked, bool boundChoice)
        {
            // A patron that cannot join because of a treaty is not an ally that declines: it is a
            // patron with two obligations and one decision. It goes down a different road entirely,
            // and the trust floor below is not consulted on the way (story 1.10d, R2).
            if (boundChoice)
            {
                BoundChoice(state, treaty, caller, ally, enemy);
                return false;
            }

            if (IsPlayerDecision(ally))
            {
                AskPlayer(state, treaty, caller, ally, enemy);
                return false;
            }

            if (WouldAnswer(state, treaty, caller, ally, enemy, callerWasAttacked, out var why))
            {
                Answer(state, treaty, caller, ally, enemy);
                return true;
            }

            Refuse(state, treaty, caller, ally, why);
            return false;
        }

        private static string Role(Treaty treaty, Kingdom party)
            => IsServingVassal(treaty, party) ? "vassal" : IsProtectingPatron(treaty, party) ? "patron" : "ally";

        /// <summary>The vassal's side of a vassalage, as opposed to the patron's.</summary>
        private static bool IsServingVassal(Treaty treaty, Kingdom party)
            => treaty.SubordinatesForeignPolicy && treaty.SubordinateParty == party;

        /// <summary>The patron's side of a vassalage.</summary>
        private static bool IsProtectingPatron(Treaty treaty, Kingdom party)
            => treaty.SubordinatesForeignPolicy && treaty.SubordinateParty != null
               && treaty.SubordinateParty != party;

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
            List<Treaty> obligations, bool quiet = false)
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
                if (quiet) continue;
                Log.Debug("CallToArms", vassalages[i].SubordinateParty.Name
                                        + " is not called: " + caller.Name + " may raise "
                                        + allowed + " of " + vassalages.Count + " vassals for this war.");
            }
        }

        /// <summary>
        /// Whether this obligation puts <paramref name="ally"/> into <paramref name="caller"/>'s
        /// war against <paramref name="enemy"/>, and whether the only thing in the way is a treaty
        /// it is now offered a choice about.
        ///
        /// **A patron bound to its vassal's attacker is a choice, not a skip** (story 1.10d, R1).
        /// This used to return false for it on one principle - an obligation cannot override a
        /// standing agreement, so the pact it already holds with the enemy wins. That is right for
        /// an ally, and it was wrong for a patron, because the principle then charged it half a
        /// protection term for a decision nobody ever offered it. It is put the choice instead, and
        /// the full price of breaking every treaty with the attacker comes with it (R2, R3).
        ///
        /// For every ally that is not a protecting patron the old rule stands untouched.
        /// </summary>
        private static bool Applies(ModState state, Treaty treaty, Kingdom caller, Kingdom ally,
            Kingdom enemy, bool callerWasAttacked, out bool boundChoice)
        {
            boundChoice = false;
            if (ally == null || ally == enemy || ally.IsEliminated) return false;
            if (ally.IsAtWarWith(enemy)) return false;

            // A defensive pact never drags anyone into a war of conquest.
            if (treaty.CallToArmsIsDefensiveOnly && !callerWasAttacked) return false;

            // Before the treaty test, not after: a patron is only protecting anybody when its
            // vassal was attacked, and one that is not must never be asked the question.
            //
            // The two directions of a vassalage owe different things. The vassal marches in
            // its patron's wars, offensive or defensive. The patron owes protection: it is
            // called when its vassal is attacked and never into a war the vassal started.
            //
            // This used to read "a vassal is called by its patron, not the other way round",
            // and so the patron was never called at all. Design 04 §4.3 makes protection the
            // patron's side of the bargain and Hold already punished its absence, but nothing
            // ever asked a patron to provide it - Hold measured a duty no code could fulfil,
            // which is a large part of why run 04's links sat at 15-36.
            if (IsProtectingPatron(treaty, ally) && !callerWasAttacked) return false;

            if (!state.HasTreatyForbiddingWar(ally, enemy)) return true;

            if (IsProtectingPatron(treaty, ally) && CanChooseSide(state, ally, enemy))
            {
                boundChoice = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Whether this is the one case story 1.10d is about: a patron called to defend its vassal
        /// and stopped by a live treaty with the attacker (R1, and §3 "Out" for the two exclusions).
        /// </summary>
        private static bool CanChooseSide(ModState state, Kingdom patron, Kingdom attacker)
        {
            if (attacker == null || patron == null) return false;

            // Two clients of the same patron at war with each other (D3): the sphere's answer to
            // that is ReconcileWithSiblings, and a patron cannot side with one client against
            // another - Hegemony.AnswerTo reads it the same way.
            if (TreatyRegistry.PatronOf(state, attacker) == patron) return false;

            // Our own patron is the attacker. Refusing that oath is secession (design 04 §6.3),
            // not a choice between two agreements, so this stays legal neglect.
            if (TreatyRegistry.PatronOf(state, patron) == attacker) return false;

            return true;
        }

        private static bool IsPlayerDecision(Kingdom ally)
            => ally != null && Hero.MainHero != null && ally.Leader == Hero.MainHero;

        /// <summary>
        /// Whether an AI ally honours the call. Vassals are held to a higher bar because
        /// service is what they agreed to, but even a vassal will not march while its own
        /// realm is collapsing.
        ///
        /// A patron answering its vassal is judged by the same rules as an ally, trust floor
        /// included, and that is deliberate: a patron that no longer trusts its vassal - one
        /// that refused its summons or treated with outsiders behind its back - leaves it to
        /// fight alone. Protection is conditional on service, which is what makes the two
        /// sides of the bargain answer each other.
        /// </summary>
        public static bool WouldAnswer(ModState state, Treaty treaty, Kingdom caller, Kingdom ally,
            Kingdom enemy, bool callerWasAttacked, out string why)
        {
            if (!WillingToAnswer(state, treaty, caller, ally, out why)) return false;

            // A vassal marching for its patron does not weigh the odds - Hold already decided
            // whether it serves, and service is what it owes.
            if (IsServingVassal(treaty, ally)) return true;

            // Joining a war that cannot be won is not loyalty, it is suicide - but "cannot be
            // won" is a judgment about both whole sides, not about the two kingdoms in this
            // conversation. It used to compare the enemy with the caller and this one ally,
            // so against a dominant power every ally refused in turn, each reckoning it would
            // stand alone: a coalition dissolved exactly when it was attacked, which is
            // bandwagoning, the opposite of what an alliance against the strong is for.
            var ourSide = Power.Strength(caller) + Power.Strength(ally)
                          + ExpectedSupport(state, caller, enemy, callerWasAttacked, exclude: ally);
            var theirSide = Power.Strength(enemy)
                            + ExpectedSupport(state, enemy, caller, !callerWasAttacked);
            if (theirSide > ourSide * DiplomacyConstants.CallToArmsHopelessRatio)
            {
                why = enemy.Name + " and those who would stand with it are too strong for our side ("
                      + theirSide.ToString("0") + " against " + ourSide.ToString("0") + ")";
                return false;
            }

            why = null;
            return true;
        }

        /// <summary>
        /// Everything in <see cref="WouldAnswer"/> except the odds: whether this party is
        /// able and inclined to honour the obligation at all. Split out so the odds can ask
        /// the same question of everyone else on the same side without asking about odds
        /// again, which would never end.
        /// </summary>
        private static bool WillingToAnswer(ModState state, Treaty treaty, Kingdom caller, Kingdom ally,
            out string why)
        {
            var worstExhaustion = WarExhaustion.Worst(state, ally);
            if (worstExhaustion > DiplomacyConstants.CallToArmsRefuseAboveExhaustion)
            {
                why = "already fighting for its life (exhaustion " + worstExhaustion.ToString("0.0") + ")";
                return false;
            }

            if (IsServingVassal(treaty, ally))
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

            why = null;
            return true;
        }

        /// <summary>
        /// The strength <paramref name="principal"/> can expect beside it in a war with
        /// <paramref name="opponent"/>: every kingdom bound to it whose obligation applies and
        /// who would be willing to honour it, plus any already at war with the opponent. Live
        /// strength - this is about a war now.
        ///
        /// One resolver for "who stands with whom", read by two questions that used to ignore
        /// it: whether an ally thinks a war is hopeless, and what an aggressor thinks it is
        /// taking on. The second is what lets an alliance deter rather than only escalate - a
        /// kingdom choosing a target now sees the coalition behind it.
        ///
        /// The vassal cap applies, and willingness is judged without the odds (see
        /// <see cref="WillingToAnswer"/>). The player is estimated by the same rules as anyone;
        /// what the player actually does is their own business.
        /// </summary>
        public static float ExpectedSupport(ModState state, Kingdom principal, Kingdom opponent,
            bool principalWasAttacked, Kingdom exclude = null)
        {
            if (state == null || principal == null || opponent == null || principal == opponent) return 0f;

            var obligations = new List<Treaty>();
            foreach (var treaty in TreatyRegistry.AlliesOf(state, principal)) obligations.Add(treaty);
            CapVassalCascade(state, principal, opponent, obligations, quiet: true);

            var counted = new HashSet<Kingdom>();
            var total = 0f;
            for (var i = 0; i < obligations.Count; i++)
            {
                var treaty = obligations[i];
                var ally = treaty.Other(principal);
                if (ally == null || ally == exclude || ally == opponent || ally.IsEliminated) continue;
                if (counted.Contains(ally)) continue;

                // Already fighting the opponent: on our side of this whatever the paperwork says.
                var applies = Applies(state, treaty, principal, ally, opponent, principalWasAttacked,
                    out var boundChoice);
                var stands = ally.IsAtWarWith(opponent)
                             || (applies && !boundChoice && WillingToAnswer(state, treaty, principal, ally, out _));

                // A patron that is only *offered* the choice is not counted as standing. An
                // estimate of support must never assume a treaty gets torn up to win an argument -
                // and asking it would recurse straight back through Hegemony.WouldHonourVassal,
                // which weighs this very war. It joins when it has actually chosen.
                if (boundChoice && !stands) continue;

                if (!stands) continue;

                counted.Add(ally);
                total += Power.Strength(ally);
            }
            return total;
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
            Telemetry.Event("call_to_arms", "caller", caller, "ally", ally, "enemy", enemy,
                "treaty", treaty.Type, "role", Role(treaty, ally), "outcome", "answered");

            Log.Info("CallToArms", ally.Name + " answered " + caller.Name
                                   + " and declared war on " + enemy.Name + ".");
            Announce(DiText.T("DI_CALLTOARMS_ALLY_HONOURS",
                        "{ALLY} honours its {TREATY} with {CALLER} and joins the war against {ENEMY}.",
                        ("ALLY", ally.Name),
                        ("TREATY", Models.Treaty.NameOf(treaty.Type)),
                        ("CALLER", caller.Name),
                        ("ENEMY", enemy.Name)),
                Colors.Green);
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
                Telemetry.Event("call_to_arms", "caller", caller, "ally", ally, "treaty", treaty.Type,
                    "role", Role(treaty, ally), "outcome", "excused", "reason", why.Substring(Excused.Length));
                Log.Info("CallToArms", ally.Name + " could not answer " + caller.Name
                                       + " - " + why.Substring(Excused.Length) + ".");
                return;
            }

            TrustRegistry.OnCallToArmsRefused(state, caller, ally);
            Telemetry.Event("call_to_arms", "caller", caller, "ally", ally, "treaty", treaty.Type,
                "role", Role(treaty, ally), "outcome", IsServingVassal(treaty, ally) ? "defied" : "refused",
                "reason", why);

            if (IsServingVassal(treaty, ally))
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

            // A patron that stays out breaks nothing - protection is not service, and the
            // price is already paid where it belongs: the vassal's trust just fell, and Hold
            // reads both that and the unanswered war every day it stays unanswered.
            Announce(IsProtectingPatron(treaty, ally)
                    ? DiText.T("DI_CALLTOARMS_ALLY_LEAVES_VASSAL",
                        "{ALLY} leaves its vassal {CALLER} to fight alone.",
                        ("ALLY", ally.Name),
                        ("CALLER", caller.Name))
                    : DiText.T("DI_CALLTOARMS_ALLY_REFUSES",
                        "{ALLY} refuses to honour its {TREATY} with {CALLER}.",
                        ("ALLY", ally.Name),
                        ("TREATY", Models.Treaty.NameOf(treaty.Type)),
                        ("CALLER", caller.Name)),
                Colors.Yellow);
        }

        // ================= The bound patron's choice (story 1.10d) ==============

        /// <summary>
        /// A patron is called to defend its vassal and a live treaty with the attacker forbids it.
        /// Two answers, and the one taken is decided in one place for everybody (R2/R8).
        ///
        /// Runs inside <c>_issuing</c> like every other answer, so the patron's own allies and
        /// vassals are not called into this war by its joining (R5) - the same one-step rule the
        /// cascade cap exists to enforce.
        /// </summary>
        private static void BoundChoice(ModState state, Treaty treaty, Kingdom vassal, Kingdom patron, Kingdom attacker)
        {
            try
            {
                // Collected first: the same list prices the question, breaks the treaties and goes
                // into the log, so the patron cannot be quoted one price and charged another.
                var bindings = new List<Treaty>();
                for (var i = 0; i < state.Treaties.Count; i++)
                {
                    var live = state.Treaties[i];
                    if (live.IsActive && live.ForbidsWar && live.IsBetween(patron, attacker)) bindings.Add(live);
                }

                if (bindings.Count == 0)
                {
                    Log.Debug("CallToArms", patron.Name + " was offered the choice against " + attacker.Name
                                            + " and no treaty stands after all.");
                    return;
                }

                // R1: the choice replaces only the treaty test. A patron too spent to answer any
                // call is not asked this one either - the unbound path refuses it in
                // WillingToAnswer, and the bound path skipped that until the review of 2026-10-01,
                // so an exhausted player was asked to tear up a treaty for a war it would not fight.
                var exhaustion = WarExhaustion.Worst(state, patron);
                if (exhaustion > DiplomacyConstants.CallToArmsRefuseAboveExhaustion)
                {
                    Log.Info("CallToArms", "[PROTECT] not offered patron=" + patron.Name + " attacker=" + attacker.Name
                                            + " - already fighting for its life (exhaustion " + exhaustion.ToString("0.0") + ")");
                    return;
                }

                Log.Info("CallToArms", "[PROTECT] bound-choice patron=" + patron.Name + " vassal=" + vassal.Name
                                        + " attacker=" + attacker.Name + " treaties=" + Describe(bindings));
                Telemetry.Event("bound_choice", "patron", patron, "vassal", vassal, "attacker", attacker,
                    "treaties", Describe(bindings).Replace(", ", "+"), "bindingCount", bindings.Count,
                    "hold", Hegemony.HoldOf(treaty));

                if (IsPlayerDecision(patron))
                {
                    AskBoundChoice(state, treaty, patron, vassal, attacker, bindings);
                    return;
                }

                var honour = Hegemony.WouldHonourVassal(state, treaty, attacker, out var why);
                if (!honour)
                {
                    Log.Info("CallToArms", "[PROTECT] answer=honour-treaty patron=" + patron.Name
                                            + " attacker=" + attacker.Name + " - " + why);
                    Telemetry.Event("bound_choice", "patron", patron, "vassal", vassal, "attacker", attacker,
                        "outcome", "honour-treaty", "reason", why);
                    TellVassal(patron, vassal, false);
                    return;
                }

                HonourVassal(state, treaty, patron, vassal, attacker, bindings, "ai");
            }
            catch (System.Exception ex)
            {
                // AC8: a failure here must not leave the patron half-committed. Nothing has been
                // broken at this point, and the war it did not join reads as legal neglect on its
                // own tomorrow - which is what staying out means.
                Log.Error("CallToArms", "The bound patron's choice failed.", ex);
            }
        }

        /// <summary>
        /// Honour the vassal: break **every** treaty standing between the patron and the attacker,
        /// then join the war (R2).
        ///
        /// **Order is the whole of atomicity here** (R4). Every treaty goes first, in one pass:
        /// breaking them after the war was declared would mean the backstops
        /// (<see cref="TreatyEnforcement.WhyWarActionRefused"/>) could refuse the war after the
        /// trust was already paid, and the patron would have torn up its alliances for nothing.
        ///
        /// The cost is not discounted (D2). A cheaper breach would make vassalage a way to tear up
        /// any treaty cheaply, and the full price is what makes the choice weigh at all.
        /// </summary>
        private static bool HonourVassal(ModState state, Treaty treaty, Kingdom patron, Kingdom vassal,
            Kingdom attacker, List<Treaty> bindings, string source)
        {
            var broken = new List<string>();
            for (var i = 0; i < bindings.Count; i++)
            {
                var live = bindings[i];
                var type = live.Type;
                TreatyRegistry.Break(state, live, patron);
                if (!live.IsActive) broken.Add(type.ToString());
            }

            // Declared as an answer to a call, which is the most legitimate reason the engine knows:
            // honouring a bond is never an act of aggression.
            if (!FailNextBoundWar)
                DeclareWarAction.ApplyByCallToWarAgreement(patron, attacker);

            if (FailNextBoundWar || !patron.IsAtWarWith(attacker))
            {
                // The treaties are gone and the war is not. The log has to name exactly what was
                // torn up, because the world now shows a patron that broke its word and got
                // nothing for it (AC4).
                Log.Info("CallToArms", "[PROTECT] answer=honour-vassal FAILED patron=" + patron.Name
                                        + " attacker=" + attacker.Name + " broke " + Describe(broken)
                                        + " and the war was refused; counted as honour-the-treaty"
                                        + (FailNextBoundWar ? " (forced by the test lever)" : "") + ".");
                Telemetry.Event("bound_choice", "patron", patron, "vassal", vassal, "attacker", attacker,
                    "outcome", "war-refused", "broken", Describe(broken).Replace(", ", "+"));
                TellVassal(patron, vassal, false);
                return false;
            }

            // Tag the war the ledger just opened, so the patron is released when its vassal makes
            // peace - the same tag Answer puts on an ally's war.
            var joined = state.OngoingWarBetween(patron, attacker);
            if (joined != null) joined.MarkCalledBy(vassal);

            Log.Info("CallToArms", "[PROTECT] answer=honour-vassal patron=" + patron.Name
                                    + " attacker=" + attacker.Name + " broke " + Describe(broken)
                                    + " and joined its vassal's war.");
            Telemetry.Event("bound_choice", "patron", patron, "vassal", vassal, "attacker", attacker,
                "outcome", "honour-vassal", "source", source, "broken", Describe(broken).Replace(", ", "+"),
                "hold", Hegemony.HoldOf(treaty));
            TellVassal(patron, vassal, true);
            Announce(patron.Name + " tears up its " + DescribeForPlayer(bindings) + " with " + attacker.Name
                     + " to defend " + vassal.Name + ".", Colors.Red);
            return true;
        }

        /// <summary>
        /// The patron is the player's, so it is asked (R9) with the full price of each answer:
        /// every treaty that would go, what it costs in trust, the claim the attacker gains and the
        /// legitimacy the crown spends, and what staying out costs the link in Hold.
        ///
        /// Timeout answers with the treaty, not with the vassal: silence must not break a treaty the
        /// player never agreed to break, and staying out is exactly what happens today (R7).
        /// </summary>
        private static void AskBoundChoice(ModState state, Treaty treaty, Kingdom patron, Kingdom vassal,
            Kingdom attacker, List<Treaty> bindings)
        {
            // What breaking actually charges, read from the constants TreatyRegistry.Break charges by -
            // not the AI's valuation terms. Those weigh a treaty by its worth and a legitimacy loss at
            // a share, which is fine for deciding, and was wrong for telling: the first build told a
            // player "-18 trust" for a truce and "5 legitimacy" for a breach that took 35 and 20
            // (CLAUDE.md §3: a number shown is the number used).
            var terms = Hegemony.BoundChoiceTermsOf(state, treaty, attacker);
            var intrigue = Core.Settings.Current.EnableIntrigue;
            var cost = new System.Text.StringBuilder();
            for (var i = 0; i < bindings.Count; i++)
            {
                cost.Append("  ").Append(TreatyName(bindings[i].Type)).Append(": ")
                    .Append(DiplomacyConstants.TrustTreatyBrokenVictim.ToString("0"))
                    .Append(" trust with ").Append(attacker.Name)
                    .Append(", ").Append(DiplomacyConstants.TrustTreatyBrokenObserver.ToString("0"))
                    .Append(" with every other court, a claim against you");
                if (intrigue)
                    cost.Append(", and -").Append(Intrigue.IntrigueConstants.LegitimacyBrokeTreaty.ToString("0"))
                        .Append(" legitimacy");
                cost.AppendLine();
            }
            if (terms.TributeLost > 0f)
                cost.Append("  and the tribute ").Append(attacker.Name).AppendLine(" pays you ends with it");

            var neglect = (DiplomacyConstants.HoldLegalNeglectShare * DiplomacyConstants.HoldProtectionWeight).ToString("0.0");
            var body = attacker.Name + " is attacking " + vassal.Name + ", and the " + TreatyName(bindings[0].Type)
                       + " you hold with " + attacker.Name + " forbids you to join."
                       + System.Environment.NewLine + System.Environment.NewLine
                       + "Defend the vassal - break:" + System.Environment.NewLine + cost
                       + System.Environment.NewLine + "Honour the treaty - stay out. " + vassal.Name + " fights alone, "
                       + "and the war counts as legal neglect: "
                       + neglect + " of an ignored war off its hold, every day it runs."
                       + System.Environment.NewLine + System.Environment.NewLine
                       + "The choice is yours once. You can still change course later by breaking the "
                       + "treaty and declaring the war yourself; hold follows the world either way.";

            void Honour()
            {
                try { HonourVassal(state, treaty, patron, vassal, attacker, bindings, "player"); }
                catch (System.Exception ex) { Log.Error("CallToArms", "Honouring the vassal failed.", ex); }
            }

            // Two callers, two different facts: a ruler who clicked "Honour the treaty", and a prompt
            // nobody answered. The first build logged both as "declined by the ruler" - live on
            // 2026-10-01 the prompt closed itself 24.0 real seconds after it opened, unanswered,
            // and the log said the player had chosen.
            void Stay(string how, string source)
            {
                try
                {
                    Log.Info("CallToArms", "[PROTECT] answer=honour-treaty patron=" + patron.Name
                                            + " attacker=" + attacker.Name
                                            + " - " + how + ".");
                    Telemetry.Event("bound_choice", "patron", patron, "vassal", vassal, "attacker", attacker,
                        "outcome", "honour-treaty", "source", source);
                    TellVassal(patron, vassal, false);
                }
                catch (System.Exception ex)
                {
                    Log.Error("CallToArms", "Recording the refusal to defend failed.", ex);
                }
            }

            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    "Break your " + TreatyName(bindings[0].Type) + " with " + attacker.Name + "?",
                    body,
                    true, true,
                    "Break it and defend " + vassal.Name, "Honour the treaty",
                    Honour, () => Stay("declined by the ruler", "player"), "",
                    DiplomacyConstants.CallToArmsPlayerResponseSeconds,
                    () => Stay("no answer before the prompt expired", "timeout"), null), true);
            }
            catch (System.Exception ex)
            {
                Log.Error("CallToArms", "Could not show the bound-patron prompt.", ex);
                Stay("the prompt could not be shown", "error");
            }
        }

        /// <summary>
        /// The vassal is told which side its patron chose (R10). It matters to it: one answer
        /// spends the patron's alliances and the other leaves it to be beaten. Spoken to the player
        /// only when the player rules that vassal - everyone else reads it in the log.
        /// </summary>
        private static void TellVassal(Kingdom patron, Kingdom vassal, bool joined)
        {
            var text = patron.Name + (joined
                ? " breaks its treaties and joins " + vassal.Name + "'s war."
                : " stays out of " + vassal.Name + "'s war, whatever binds it.");
            Log.Info("CallToArms", text);
            if (vassal.Leader == Hero.MainHero) Log.Notify(text, joined ? Colors.Green : Colors.Yellow);
        }

        private static string Describe(List<Treaty> treaties)
        {
            var names = new List<string>();
            for (var i = 0; i < treaties.Count; i++) names.Add(treaties[i].Type.ToString());
            return names.Count == 0 ? "none" : string.Join(", ", names.ToArray());
        }

        /// <summary>
        /// The whole refusal sentence for a vassal who is already carrying marks, said for this
        /// refusal rather than in general: the refusal that brings the marks to two renounces it on
        /// the spot and is charged as a broken treaty, which a player asked with one mark already
        /// standing was never told - the text said "a second mark inside a year renounces" whether
        /// or not this was the second (live, 2026-10-01: trust with the patron fell 50.1 on such a
        /// refusal, not 15).
        ///
        /// Whole sentences behind keys, one per outcome, rather than a clause spliced after "and ".
        /// It was a clause first: <c>RenounceClause</c> returned English and the caller wrote it into
        /// <c>"{CLAUSE}"</c>, which is the half-in-one-language hole - a translator cannot move a
        /// clause that arrives already glued to an "and" they cannot see.
        /// </summary>
        internal static string RenounceCost(Treaty vassalage, Kingdom caller, string refusedTrust)
        {
            var marks = vassalage == null ? 0 : vassalage.DefianceMarks;
            if (marks + 1 >= DiplomacyConstants.DefianceMarksToLapse)
                return DiText.T("DI_CALLTOARMS_REFUSING_MARKS_STANDING",
                        "Refusing is defiance: it costs {TRUST} trust with {CALLER}, earns a mark, and with {MARKS} mark(s) already standing, this refusal renounces the vassalage now: a broken treaty, at {VICTIM} more trust with your patron and {OBSERVER} with every other court, and a reason for war.",
                        ("TRUST", refusedTrust),
                        ("CALLER", caller.Name),
                        ("MARKS", marks),
                        ("VICTIM", (-DiplomacyConstants.TrustTreatyBrokenVictim).ToString("0")),
                        ("OBSERVER", (-DiplomacyConstants.TrustTreatyBrokenObserver).ToString("0")));
            return DiText.T("DI_CALLTOARMS_REFUSING_SECOND_MARK",
                    "Refusing is defiance: it costs {TRUST} trust with {CALLER}, earns a mark, and a second mark inside a year renounces the vassalage, as a broken treaty.",
                    ("TRUST", refusedTrust),
                    ("CALLER", caller.Name));
        }

        /// <summary>
        /// The summons' refusal line, whole and keyed, for a vassal already carrying marks.
        ///
        /// The summons and the call to arms say the same thing in different words - "Refuse:
        /// defiance" against "Refusing is defiance" - so they need their own keys rather than one
        /// clause shared between them. Both used to splice that clause in behind an "and", which no
        /// translation can move.
        /// </summary>
        internal static string SummonsRenounceLine(Treaty vassalage, string patron, string refusedTrust)
        {
            var marks = vassalage == null ? 0 : vassalage.DefianceMarks;
            if (marks + 1 >= DiplomacyConstants.DefianceMarksToLapse)
                return DiText.T("DI_SUMMONS_REFUSING_MARKS_STANDING",
                        "Refuse: defiance. It costs {TRUST} trust with {PATRON}, earns a mark, and with {MARKS} mark(s) already standing, this refusal renounces the vassalage now: a broken treaty, at {VICTIM} more trust with your patron and {OBSERVER} with every other court, and a reason for war.",
                        ("TRUST", refusedTrust),
                        ("PATRON", patron),
                        ("MARKS", marks),
                        ("VICTIM", (-DiplomacyConstants.TrustTreatyBrokenVictim).ToString("0")),
                        ("OBSERVER", (-DiplomacyConstants.TrustTreatyBrokenObserver).ToString("0")));
            return DiText.T("DI_SUMMONS_REFUSING_SECOND_MARK",
                    "Refuse: defiance. It costs {TRUST} trust with {PATRON}, earns a mark, and a second mark inside a year renounces the vassalage, as a broken treaty.",
                    ("TRUST", refusedTrust),
                    ("PATRON", patron));
        }

        /// <summary>
        /// A treaty as a player reads it. The log and the telemetry keep the enum names, which the
        /// analysis script parses; text on screen does not show "DefensivePact" (live, 2026-10-01).
        /// </summary>
        private static string TreatyName(TreatyType type)
        {
            switch (type)
            {
                case TreatyType.NonAggressionPact: return "non-aggression pact";
                case TreatyType.DefensivePact: return "defensive pact";
                case TreatyType.Alliance: return "alliance";
                case TreatyType.TributaryPact: return "tributary pact";
                case TreatyType.Vassalage: return "vassalage";
                default: return "truce";
            }
        }

        private static string DescribeForPlayer(List<Treaty> treaties)
        {
            var names = new List<string>();
            for (var i = 0; i < treaties.Count; i++) names.Add(TreatyName(treaties[i].Type));
            return names.Count == 0 ? "treaties" : string.Join(" and ", names.ToArray());
        }

        private static string Describe(List<string> names)
            => names.Count == 0 ? "nothing" : string.Join(", ", names.ToArray());

        /// <summary>
        /// Set by <c>diplomacy.test_fail_bound_war</c> so AC4 can be checked: the treaties go, the
        /// war does not, and the log has to say so. Session state, not saved, like every other test
        /// lever's memory.
        /// </summary>
        internal static bool FailNextBoundWar;

        /// <summary>
        /// Puts the choice to the player when they rule the called kingdom. Letting it
        /// expire counts as a refusal, because silence is an answer.
        /// </summary>
        private static void AskPlayer(ModState state, Treaty treaty, Kingdom caller, Kingdom ally, Kingdom enemy)
        {
            var refusedTrust = (-DiplomacyConstants.TrustCallToArmsRefused).ToString("0");
            string cost;
            string body;

            if (IsProtectingPatron(treaty, ally))
            {
                cost = "Refusing costs " + refusedTrust + " trust with " + caller.Name
                       + ", and a vassal left to fight alone loses its hold on you a little more"
                       + " every day the war goes unanswered.";
                body = "Your vassal " + caller.Name + " has been attacked by " + enemy.Name
                       + " and calls on you for the protection it pays for." + "\n\n" + cost;
            }
            else
            {
                // A refused summons is a defiance mark; the second inside a year breaks the
                // vassalage. The old text said the first refusal renounced it, which stopped
                // being true when marks were introduced.
                cost = IsServingVassal(treaty, ally)
                    ? RenounceCost(treaty, caller, refusedTrust)
                    : DiText.T("DI_CALLTOARMS_REFUSING_COSTS",
                        "Refusing costs {TRUST} trust with {CALLER}, and the {TREATY} will lapse.",
                        ("TRUST", refusedTrust),
                        ("CALLER", caller.Name),
                        ("TREATY", Models.Treaty.NameOf(treaty.Type)));
                body = DiText.T("DI_CALLTOARMS_INVOKES",
                        "{CALLER} invokes its {TREATY} and calls you to war against {ENEMY}.\n\n{cost}",
                        ("CALLER", caller.Name),
                        ("TREATY", Models.Treaty.NameOf(treaty.Type)),
                        ("ENEMY", enemy.Name),
                        ("cost", cost));
            }

            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    DiText.T("DI_DIPLOMACY_CALL_TO_ARMS_2", "Call to Arms"),
                    body,
                    true, true,
                    DiText.T("DI_DIPLOMACY_HONOUR_IT_2", "Honour it"), DiText.T("DI_DIPLOMACY_REFUSE", "Refuse"),
                    () => Answer(state, treaty, caller, ally, enemy),
                    () => Refuse(state, treaty, caller, ally, "declined by the ruler"),
                    "", DiplomacyConstants.CallToArmsPlayerResponseSeconds,
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


        private static void Announce(string text, Color color)
        {
            if (!Settings.Current.AnnounceAiDecisions) return;
            Log.Notify(text, color);
        }
    }
}
