using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// Signs, maintains and terminates treaties. The whole lifecycle lives here so that
    /// the trust consequences of each ending can never be forgotten at a call site:
    /// expiry pays a dividend, a breach costs reputation, and both happen in one place.
    ///
    /// As with claims, the AI and the player go through the same functions.
    /// </summary>
    public static class TreatyRegistry
    {
        // ----- Signing --------------------------------------------------------

        /// <summary>
        /// Returns false with a reason when the two parties cannot sign this treaty now.
        /// The reason string is player-facing: it is what the UI in 1.8 will show, and what
        /// the console prints today.
        /// </summary>
        /// <param name="replacing">
        /// A live treaty to treat as already gone, for a transfer that ends one bond and signs
        /// another in the same act - a vassal poached from its patron. Asking with the old bond
        /// still in force would refuse on it; asking only after tearing it up would find out
        /// too late.
        /// </param>
        /// <param name="settlesWar">
        /// The treaty is a term of the settlement that ends the war between the parties - a
        /// tribute or vassalage imposed at the peace table, or a vassal defecting to its
        /// attacker. Two things follow.
        ///
        /// The war being still open is not a refusal: the peace table asks whether what it is
        /// promising could be signed afterwards, while the war is formally running, and "Make
        /// peace first." is the step being planned.
        ///
        /// The trust floor does not apply, for the reason the truce is exempt: ending a war
        /// has to stay possible however the parties feel about each other, and a loser does
        /// not need to trust a winner to be made to kneel. This was always latent - the floor
        /// silently voided imposed terms after the peace - but since run 06 a war drags a
        /// neutral pair under the floor in about a month
        /// (<see cref="DiplomacyConstants.TrustDecayWarPerDay"/>), so without the exemption
        /// the peace table could almost never impose a treaty at all.
        /// </param>
        public static bool CanSign(ModState state, Kingdom a, Kingdom b, TreatyType type, out string reason,
            Treaty replacing = null, bool settlesWar = false)
        {
            reason = null;

            if (a == null || b == null || a == b) { reason = "Two different kingdoms are required."; return false; }
            if (a.IsEliminated || b.IsEliminated) { reason = "One of the kingdoms no longer exists."; return false; }

            var existing = state.ActiveTreatyBetween(a, b, type);
            if (existing != null) { reason = "That treaty already exists."; return false; }

            // A truce is how a war ends, so it is the one type that requires a war and is
            // never blocked by distrust - otherwise a badly behaved kingdom could never
            // make peace at all.
            var atWar = a.IsAtWarWith(b);
            if (type == TreatyType.Truce)
            {
                if (!atWar) { reason = "A truce needs a war to end."; return false; }
                return true;
            }

            if (atWar && !settlesWar) { reason = "Make peace first."; return false; }

            if (state.HasTreatyForbiddingWar(a, b) && type != TreatyType.Alliance
                && type != TreatyType.DefensivePact && type != TreatyType.Vassalage
                && type != TreatyType.TributaryPact)
            {
                reason = "An agreement between these kingdoms already covers this.";
                return false;
            }

            if (!settlesWar && !TrustRegistry.WillConsiderPacts(state, b, a))
            {
                reason = b.Name + " does not trust " + a.Name + " enough to sign anything but a truce.";
                return false;
            }
            if (!settlesWar && !TrustRegistry.WillConsiderPacts(state, a, b))
            {
                reason = a.Name + " does not trust " + b.Name + " enough to sign anything but a truce.";
                return false;
            }

            // An alliance cannot be signed with someone at war with an existing ally: it
            // would oblige us to both sides of the same war.
            if (type == TreatyType.Alliance && ConflictsWithExistingAlliance(state, a, b, out var blocker))
            {
                reason = b.Name + " is at war with our ally " + blocker.Name + ".";
                return false;
            }
            if (type == TreatyType.Alliance && ConflictsWithExistingAlliance(state, b, a, out blocker))
            {
                reason = a.Name + " is at war with their ally " + blocker.Name + ".";
                return false;
            }

            // Vassalage and tribute both subordinate one party; being someone else's vassal
            // already spends that, so it cannot be spent twice.
            if ((type == TreatyType.Vassalage || type == TreatyType.TributaryPact)
                && PatronOf(state, b, replacing) != null)
            {
                reason = b.Name + " is already subordinate to another kingdom.";
                return false;
            }

            // Hegemony is flat: one patron per vassal, no chains. The engine has two faction
            // tiers and no parent-of-kingdom slot, so a patron of patrons is not something the
            // game could draw, and the call-to-arms cascade would have no bound.
            if (type == TreatyType.Vassalage && Hegemony.IsHegemon(state, b))
            {
                reason = b.Name + " holds vassals of its own and cannot itself submit.";
                return false;
            }

            // The other half of the same rule: a vassal cannot take one. Only the half above
            // was here, so every route that did not check the patron itself could build a
            // chain - the peace table let a defiant vassal impose vassalage on a kingdom it
            // beat, and the run-06 defection route could hand a vassal to an attacker that was
            // itself a vassal. Asked here so that no route has to remember it.
            var patronsOwnLink = type == TreatyType.Vassalage ? Hegemony.VassalageOf(state, a) : null;
            if (patronsOwnLink != null)
            {
                reason = a.Name + " answers to " + patronsOwnLink.DominantParty.Name
                         + " and cannot take vassals of its own.";
                return false;
            }

            // A vassal owes its foreign policy, and that is only now enforced for treaties -
            // until this, the prohibition existed for war alone. A badly held vassal ignores
            // it, which is the design's middle tier of defiance rather than a loophole.
            if (IsForbiddenByPatron(state, a, b, type, replacing, out reason)) return false;
            if (IsForbiddenByPatron(state, b, a, type, replacing, out reason)) return false;

            return true;
        }

        /// <summary>
        /// Whether <paramref name="signatory"/> may sign this with <paramref name="other"/> at
        /// all, given who it answers to.
        ///
        /// A truce is always exempt: stopping a war has to stay possible, whoever is whose
        /// vassal. So is anything signed with the patron itself.
        /// </summary>
        private static bool IsForbiddenByPatron(ModState state, Kingdom signatory, Kingdom other,
            TreatyType type, Treaty replacing, out string reason)
        {
            reason = null;
            if (type == TreatyType.Truce) return false;

            var vassalage = Hegemony.VassalageOf(state, signatory);
            if (vassalage == null || vassalage == replacing) return false;

            var patron = vassalage.DominantParty;
            if (patron == null || patron == other) return false;

            // Defiance: the bond is weak enough that the vassal simply does as it likes. The
            // cost lands in Sign, where the act actually happens.
            if (Hegemony.WillDefyForeignPolicy(vassalage)) return false;

            reason = signatory.Name + " answers to " + patron.Name
                     + " and cannot sign with outsiders on its own account.";
            return true;
        }

        /// <summary>
        /// Signs the treaty. <paramref name="tributePayer"/> and <paramref name="tributeAmount"/>
        /// only apply to the types that carry payment. Returns null when
        /// <see cref="CanSign"/> would refuse.
        /// </summary>
        public static Treaty Sign(ModState state, Kingdom a, Kingdom b, TreatyType type,
            out string reason, Kingdom tributePayer = null, int tributeAmount = 0, bool settlesWar = false)
        {
            if (!CanSign(state, a, b, type, out reason, settlesWar: settlesWar)) return null;

            var expiry = CampaignTime.YearsFromNow(DiplomacyConstants.TreatyDurationYears(type));
            var treaty = new Treaty(state.TakeNextTreatyId(), type, a, b, CampaignTime.Now, expiry);

            if (type == TreatyType.Vassalage || type == TreatyType.TributaryPact)
            {
                // For the asymmetric types the payer is the subordinate; if the caller did
                // not name one, default to b, which is the party being asked to submit.
                treaty.SetSubordinate(tributePayer != null && treaty.Involves(tributePayer) ? tributePayer : b);
            }

            if (tributeAmount > 0 && tributePayer != null && treaty.Involves(tributePayer))
            {
                treaty.SetTribute(tributePayer, tributeAmount,
                    CampaignTime.DaysFromNow(DiplomacyConstants.TributePeriodDays));
            }

            state.Treaties.Add(treaty);
            Telemetry.Event("treaty_signed", "type", type, "a", a, "b", b,
                "subordinate", treaty.SubordinateParty, "tribute", treaty.TributeAmount, "years",
                DiplomacyConstants.TreatyDurationYears(type));
            NoteDefiantSigning(state, a, b, type);
            NoteDefiantSigning(state, b, a, type);
            Log.Info("Treaty", "Signed: " + treaty
                               + (treaty.TributeAmount > 0
                                   ? " tribute " + treaty.TributeAmount + " from " + treaty.TributePayer.Name
                                     + " every " + DiplomacyConstants.TributePeriodDays + " days"
                                   : ""));
            return treaty;
        }

        /// <summary>
        /// A vassal that signed with an outsider anyway has defied its patron, and the patron
        /// finds out. One mark, exactly as a refused summons would be - the two are the same
        /// act of independence wearing different clothes.
        /// </summary>
        private static void NoteDefiantSigning(ModState state, Kingdom signatory, Kingdom other,
            TreatyType type)
        {
            if (type == TreatyType.Truce) return;

            var vassalage = Hegemony.VassalageOf(state, signatory);
            if (vassalage == null) return;

            var patron = vassalage.DominantParty;
            if (patron == null || patron == other) return;

            Hegemony.NoteRefusal(state, vassalage);
            TrustRegistry.Adjust(state, patron, signatory,
                DiplomacyConstants.TrustCallToArmsRefused, "treated with outsiders behind our back");

            Log.Info("Hegemony", signatory.Name + " signed a " + type + " with " + other.Name
                                 + " in defiance of its patron " + patron.Name + ".");
        }

        /// <summary>
        /// Records the truce that comes with a peace settlement.
        ///
        /// Separate from <see cref="Sign"/> because that requires an active war for a truce,
        /// and by the time the peace event fires the war is already over. A truce is never
        /// refused on trust grounds: stopping a war has to remain possible however badly
        /// the parties have behaved.
        /// </summary>
        public static Treaty SignTruceOnPeace(ModState state, Kingdom a, Kingdom b)
        {
            if (a == null || b == null || a == b) return null;
            if (state.ActiveTreatyBetween(a, b, TreatyType.Truce) != null) return null;

            var treaty = new Treaty(state.TakeNextTreatyId(), TreatyType.Truce, a, b,
                CampaignTime.Now, CampaignTime.YearsFromNow(DiplomacyConstants.TruceYears));
            state.Treaties.Add(treaty);
            Telemetry.Event("treaty_signed", "type", TreatyType.Truce, "a", a, "b", b, "years", DiplomacyConstants.TruceYears);
            return treaty;
        }

        // ----- Termination ----------------------------------------------------

        /// <summary>
        /// One party walks away from a live treaty. This is always allowed - the mod never
        /// takes the option away - but it is expensive, and the cost is reputational rather
        /// than mechanical so that it lasts.
        /// </summary>
        public static void Break(ModState state, Treaty treaty, Kingdom breaker)
        {
            if (treaty == null || !treaty.IsActive || !treaty.Involves(breaker)) return;

            var victim = treaty.Other(breaker);
            treaty.Close(TreatyStatus.Broken, breaker);
            Telemetry.Event("treaty_broken", "type", treaty.Type, "breaker", breaker, "victim", victim,
                "daysInForce", (float)(CampaignTime.Now - treaty.SignedOn).ToDays);

            TrustRegistry.OnTreatyBroken(state, treaty, breaker);
            ClaimRegistry.GrantBrokenTreatyClaim(state, victim, breaker);

            Log.Info("Treaty", breaker.Name + " broke " + treaty.Type + " with " + victim.Name
                               + " - " + victim.Name + " now has a casus belli.");
        }

        /// <summary>
        /// Ends a treaty as broken by <paramref name="breaker"/> and charges nothing for it.
        ///
        /// For the agreements swept up in a larger breach that has already been paid for in
        /// full. A vassal revolting against its patron repudiates its oath *and* whatever
        /// else stood between them; run 04 found a revolt silently refused because a
        /// DefensivePact with the same patron survived the broken vassalage and vetoed the
        /// war of independence. Charging the trust penalty and granting a casus belli once
        /// per torn-up page would price one act as several, so the headline breach pays and
        /// these follow it.
        ///
        /// Also how a poached vassal leaves its old patron: the cost of that act lands on the
        /// poacher (trust, relation, a casus belli and a war), not a second time on the client.
        ///
        /// Closed as <see cref="TreatyStatus.Broken"/> rather than dissolved because the
        /// save record has to stay honest: nobody consented to this.
        /// </summary>
        public static void RepudiateAlongside(ModState state, Treaty treaty, Kingdom breaker)
        {
            if (treaty == null || !treaty.IsActive || !treaty.Involves(breaker)) return;

            var victim = treaty.Other(breaker);
            treaty.Close(TreatyStatus.Broken, breaker);
            Telemetry.Event("treaty_repudiated", "type", treaty.Type, "breaker", breaker, "victim", victim);
            Log.Info("Treaty", breaker.Name + " repudiated its " + treaty.Type + " with "
                               + victim.Name + " - no separate charge; the act it belongs to carries the cost.");
        }

        /// <summary>Both parties agree to end it early. No penalty, no claim.</summary>
        public static void Dissolve(ModState state, Treaty treaty)
        {
            if (treaty == null || !treaty.IsActive) return;
            treaty.Close(TreatyStatus.Dissolved);
            Telemetry.Event("treaty_dissolved", "type", treaty.Type, "a", treaty.PartyA, "b", treaty.PartyB);
            Log.Info("Treaty", "Dissolved by mutual consent: " + treaty);
        }

        // ----- Daily upkeep ---------------------------------------------------

        /// <summary>
        /// Expires treaties that have run out and pays the trust dividend for seeing one
        /// through. Running its full term is the cheapest reputation a kingdom can buy.
        /// </summary>
        public static int ExpireAndReward(ModState state)
        {
            var expired = 0;
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (!treaty.IsActive || !treaty.HasRunOut) continue;

                treaty.Close(TreatyStatus.Expired);
                Telemetry.Event("treaty_expired", "type", treaty.Type, "a", treaty.PartyA, "b", treaty.PartyB,
                    "hold", treaty.Type == TreatyType.Vassalage ? Hegemony.HoldOf(treaty) : 0f);
                TrustRegistry.OnTreatyHonoured(state, treaty);
                Log.Info("Treaty", "Expired, honoured in full: " + treaty);
                expired++;
            }
            return expired;
        }

        /// <summary>
        /// Moves tribute for every treaty whose payment is due. A payer that cannot cover it
        /// is in default: the treaty breaks and the creditor gains a casus belli, which is
        /// exactly how tribute becomes a live political problem rather than a line item.
        /// </summary>
        public static void PayDueTribute(ModState state)
        {
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (!treaty.IsActive || treaty.TributeAmount <= 0) continue;
                if (treaty.NextTributeDue > CampaignTime.Now) continue;

                var payer = treaty.TributePayer;
                var receiver = treaty.Other(payer);
                var payerLeader = payer?.Leader;
                var receiverLeader = receiver?.Leader;

                if (payerLeader == null || receiverLeader == null)
                {
                    treaty.AdvanceTributeDate(CampaignTime.DaysFromNow(DiplomacyConstants.TributePeriodDays));
                    continue;
                }

                // Passive resistance, the cheapest form of defiance in the source document:
                // the money simply stops arriving. Not a default - they can pay, they will
                // not - so it ends nothing. But it is defiance, and it is now marked as such:
                // it used to cost the vassal nothing at all, which made keeping the money the
                // obvious move for every link below 40, and the patron had no lever against it.
                if (treaty.Type == TreatyType.Vassalage
                    && Hegemony.HoldOf(treaty) < DiplomacyConstants.HoldPassiveResistanceThreshold)
                {
                    TributeWithheldThisSession++;
                    Log.Info("Hegemony", payer.Name + " withheld its tribute from " + receiver.Name
                                         + " (hold " + Hegemony.HoldOf(treaty).ToString("0.0") + ").");

                    var sinceLastMark = treaty.LastDefianceOn == CampaignTime.Never
                        ? float.MaxValue
                        : (float)(CampaignTime.Now - treaty.LastDefianceOn).ToDays;
                    if (sinceLastMark >= DiplomacyConstants.TributeWithheldMarkIntervalDays)
                        Hegemony.NoteRefusal(state, treaty);

                    treaty.AdvanceTributeDate(CampaignTime.DaysFromNow(DiplomacyConstants.TributePeriodDays));
                    continue;
                }

                if (payerLeader.Gold < treaty.TributeAmount)
                {
                    Log.Info("Treaty", payer.Name + " defaulted on tribute to " + receiver.Name + ".");
                    Break(state, treaty, payer);
                    continue;
                }

                GiveGoldAction.ApplyBetweenCharacters(payerLeader, receiverLeader, treaty.TributeAmount, true);
                TributePaidThisSession++;
                treaty.AdvanceTributeDate(CampaignTime.DaysFromNow(DiplomacyConstants.TributePeriodDays));
            }
        }

        /// <summary>
        /// Tribute payments made and withheld this session, reported in the weekly snapshot.
        ///
        /// Run 04 could count 193 withheld payments and not a single successful one, because
        /// only withholding was logged, so it could not say whether tribute ever arrived. A
        /// log line per payment would be several hundred lines a year; two counters answer the
        /// question. Session-scoped for the same reason as the vanilla refusal counters: they
        /// describe the mod's behaviour, not the campaign, and do not belong in the save.
        /// </summary>
        public static int TributePaidThisSession { get; private set; }

        public static int TributeWithheldThisSession { get; private set; }

        /// <summary>
        /// Pays the trust dividend for a peace that has held. Uses the closed war records
        /// as the source of truth, and marks each one so the dividend is paid once.
        /// </summary>
        public static void PayPeaceDividends(ModState state)
        {
            for (var i = 0; i < state.Wars.Count; i++)
            {
                var war = state.Wars[i];
                if (war.IsOngoing || war.PeaceDividendPaid) continue;
                if (war.Aggressor == null || war.Defender == null) continue;

                var yearsAtPeace = (float)(CampaignTime.Now - war.EndedOn).ToYears;
                if (yearsAtPeace < DiplomacyConstants.PeaceDividendYears) continue;

                // Only if they have actually stayed at peace since.
                if (war.Aggressor.IsAtWarWith(war.Defender)) continue;

                war.MarkPeaceDividendPaid();
                TrustRegistry.OnPeaceHeld(state, war.Aggressor, war.Defender);
            }
        }

        // ----- Queries --------------------------------------------------------

        /// <summary>The kingdom this one answers to, or null. Vassalage only.</summary>
        public static Kingdom PatronOf(ModState state, Kingdom client, Treaty ignoring = null)
        {
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (treaty == ignoring || !treaty.IsActive || !treaty.SubordinatesForeignPolicy) continue;
                if (treaty.SubordinateParty == client) return treaty.DominantParty;
            }
            return null;
        }

        /// <summary>Every kingdom that answers to this one.</summary>
        public static void CollectClients(ModState state, Kingdom patron, List<Kingdom> into)
        {
            into.Clear();
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (!treaty.IsActive || !treaty.SubordinatesForeignPolicy) continue;
                if (treaty.DominantParty != patron) continue;
                if (treaty.SubordinateParty != null) into.Add(treaty.SubordinateParty);
            }
        }

        public static IEnumerable<Treaty> AlliesOf(ModState state, Kingdom kingdom)
        {
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (treaty.IsActive && treaty.CarriesCallToArms && treaty.Involves(kingdom))
                    yield return treaty;
            }
        }

        private static bool ConflictsWithExistingAlliance(ModState state, Kingdom kingdom,
            Kingdom candidate, out Kingdom blocker)
        {
            foreach (var treaty in AlliesOf(state, kingdom))
            {
                var ally = treaty.Other(kingdom);
                if (ally != null && ally != candidate && ally.IsAtWarWith(candidate))
                {
                    blocker = ally;
                    return true;
                }
            }
            blocker = null;
            return false;
        }
    }
}
