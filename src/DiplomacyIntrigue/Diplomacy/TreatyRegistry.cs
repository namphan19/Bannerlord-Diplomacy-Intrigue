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
        public static bool CanSign(ModState state, Kingdom a, Kingdom b, TreatyType type, out string reason)
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

            if (atWar) { reason = "Make peace first."; return false; }

            if (state.HasTreatyForbiddingWar(a, b) && type != TreatyType.Alliance
                && type != TreatyType.DefensivePact && type != TreatyType.Vassalage
                && type != TreatyType.TributaryPact)
            {
                reason = "An agreement between these kingdoms already covers this.";
                return false;
            }

            if (!TrustRegistry.WillConsiderPacts(state, b, a))
            {
                reason = b.Name + " does not trust " + a.Name + " enough to sign anything but a truce.";
                return false;
            }
            if (!TrustRegistry.WillConsiderPacts(state, a, b))
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
                && PatronOf(state, b) != null)
            {
                reason = b.Name + " is already subordinate to another kingdom.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Signs the treaty. <paramref name="tributePayer"/> and <paramref name="tributeAmount"/>
        /// only apply to the types that carry payment. Returns null when
        /// <see cref="CanSign"/> would refuse.
        /// </summary>
        public static Treaty Sign(ModState state, Kingdom a, Kingdom b, TreatyType type,
            out string reason, Kingdom tributePayer = null, int tributeAmount = 0)
        {
            if (!CanSign(state, a, b, type, out reason)) return null;

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
            Log.Info("Treaty", "Signed: " + treaty
                               + (treaty.TributeAmount > 0
                                   ? " tribute " + treaty.TributeAmount + " from " + treaty.TributePayer.Name
                                     + " every " + DiplomacyConstants.TributePeriodDays + " days"
                                   : ""));
            return treaty;
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

            TrustRegistry.OnTreatyBroken(state, treaty, breaker);
            ClaimRegistry.GrantBrokenTreatyClaim(state, victim, breaker);

            Log.Info("Treaty", breaker.Name + " broke " + treaty.Type + " with " + victim.Name
                               + " - " + victim.Name + " now has a casus belli.");
        }

        /// <summary>Both parties agree to end it early. No penalty, no claim.</summary>
        public static void Dissolve(ModState state, Treaty treaty)
        {
            if (treaty == null || !treaty.IsActive) return;
            treaty.Close(TreatyStatus.Dissolved);
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

                if (payerLeader.Gold < treaty.TributeAmount)
                {
                    Log.Info("Treaty", payer.Name + " defaulted on tribute to " + receiver.Name + ".");
                    Break(state, treaty, payer);
                    continue;
                }

                GiveGoldAction.ApplyBetweenCharacters(payerLeader, receiverLeader, treaty.TributeAmount, true);
                treaty.AdvanceTributeDate(CampaignTime.DaysFromNow(DiplomacyConstants.TributePeriodDays));
            }
        }

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
        public static Kingdom PatronOf(ModState state, Kingdom client)
        {
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (!treaty.IsActive || !treaty.SubordinatesForeignPolicy) continue;
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
