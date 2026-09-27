using System;
using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// The fiefs an internal war moved between its two sides (review R-6, decided 2026-09-27):
    /// how many each side has lost and the other still holds, which wears it down daily as an
    /// occupied fief does in a foreign war, and what a crown victory gives back.
    ///
    /// **Read from <see cref="InternalWar.Captures"/>, which the capture event writes as it
    /// happens** (<c>InternalWars.OnSettlementOwnerChanged</c>: by siege, from a house of one side
    /// to a house of the other). The first build of this read vanilla's ownership log
    /// (`ChangeSettlementOwnerLogEntry`) instead, to add no save data, with the siege flag taken
    /// from a private field by reflection and the entries kept for a span the reference assemblies
    /// cannot show. It lost on review (tech lead, 2026-09-27): two saved fields are a smaller risk
    /// than a restitution that silently does nothing when the log has aged out, and the event
    /// gives the siege detail directly. <c>FiefHistory</c> could not serve: it is kept per kingdom,
    /// and a capture inside the realm never leaves the realm.
    ///
    /// A war begun on an older build has only the captures made since this build loaded.
    /// </summary>
    public static class InternalWarFiefs
    {
        /// <summary>A fief a crown victory gives back.</summary>
        public sealed class Restitution
        {
            public Settlement Settlement;

            /// <summary>The rebel house holding it at the end.</summary>
            public Clan Holder;

            /// <summary>Who held it when the war began: the house it was first taken from.</summary>
            public Clan HeldAtStart;

            /// <summary><see cref="HeldAtStart"/> if alive and still in the realm, else the ruling clan.</summary>
            public Clan ReturnsTo;
        }

        private static bool IsCrownSide(InternalWar war, Clan clan)
            => clan != null && !clan.IsEliminated && clan.Kingdom == war.Kingdom && !war.IsRebel(clan);

        /// <summary>
        /// Fiefs this war has cost one side that the other side still holds: the internal war's
        /// count for <c>DiplomacyConstants.ExhaustionPerDayPerOccupiedFief</c>, read daily.
        ///
        /// Lost by the crown: taken from a house that was not a rebel, and held now by a rebel.
        /// Lost by the rebels: taken from a rebel house, and held now by a crown house. "Rebel" is
        /// the side a house is on today; a house that changed sides (2.6c) is counted where it
        /// stands now.
        ///
        /// "Still holds", as the constant's own comment says, and not the number of captures ever
        /// made: a fief retaken stops wearing its old holder down.
        /// </summary>
        public static int OccupiedFrom(InternalWar war, bool rebelSide)
        {
            if (war?.Captures == null) return 0;

            var count = 0;
            for (var i = 0; i < war.Captures.Count; i++)
            {
                var capture = war.Captures[i];
                var holder = capture.Fief?.OwnerClan;
                var startedRebel = capture.Clan != null && war.IsRebel(capture.Clan);

                if (rebelSide)
                {
                    if (startedRebel && IsCrownSide(war, holder)) count++;
                }
                else
                {
                    if (!startedRebel && war.IsRebel(holder) && holder?.Kingdom == war.Kingdom) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// What a crown victory gives back (decided 2026-09-27): every fief taken across the line
        /// that a rebel house holds at the end goes to the house that held it when the war began,
        /// if that house is alive and still in the realm; otherwise to the ruling clan. One
        /// resolver: the war's end applies it and `diplomacy.internal_wars` prints it for a war
        /// still running, as "if the crown won today".
        ///
        /// Fiefs a rebel house held before the war and never lost stay with it: the rebels lost the
        /// war, not their lands (design 07 §3a Q1). A fief a rebel house holds at the end is given
        /// back even when it was a rebel house's own when the war began (the crown took it, and
        /// another rebel took it back): the rule restores the map as it was, and that house held it
        /// then. A house the crown bought back over the war (2.6c) keeps what it brought: it is not
        /// a rebel at the end, and its price already counted its fiefs.
        /// </summary>
        public static List<Restitution> Plan(InternalWar war)
        {
            var plan = new List<Restitution>();
            var kingdom = war?.Kingdom;
            var ruling = kingdom?.RulingClan;
            if (kingdom == null || ruling == null || war.Captures == null) return plan;

            for (var i = 0; i < war.Captures.Count; i++)
            {
                var capture = war.Captures[i];
                var settlement = capture.Fief;
                var holder = settlement?.OwnerClan;
                if (holder == null || !war.IsRebel(holder) || holder.Kingdom != kingdom) continue;

                var heldAtStart = capture.Clan;
                var returnsTo = heldAtStart != null && !heldAtStart.IsEliminated && heldAtStart.Kingdom == kingdom
                                && heldAtStart.Leader != null
                    ? heldAtStart
                    : ruling;
                if (returnsTo == holder) continue;

                plan.Add(new Restitution
                {
                    Settlement = settlement, Holder = holder, HeldAtStart = heldAtStart, ReturnsTo = returnsTo
                });
            }
            return plan;
        }

        /// <summary>
        /// Gives back what <see cref="Plan"/> lists. Called once, when the crown wins, after the
        /// rising is gone - so every house is plainly of the realm again and no fief list of the
        /// rising has to follow.
        ///
        /// Through `ChangeOwnerOfSettlementAction.ApplyByDefault`, the call the peace table makes
        /// for a ceded fief. **Not `ApplyByKingDecision`**, which would read as the crown granting
        /// the fief: `GrievanceSources` answers that detail with a `FiefToRival` grievance for every
        /// land-hungry house at court, and this is a war's verdict, not a grant. Under the default
        /// detail the grievance system records nothing (it reads only king's decisions and sieges),
        /// so the rebel who loses the fief gets no grievance without anything being suppressed.
        /// Vanilla's own side effects of the action are not visible in the reference assemblies.
        /// </summary>
        public static int Restore(ModState state, InternalWar war)
        {
            if (state == null || war == null) return 0;

            var plan = Plan(war);
            var restored = 0;
            for (var i = 0; i < plan.Count; i++)
            {
                var r = plan[i];
                try
                {
                    var receiver = r.ReturnsTo.Leader;
                    if (receiver == null) continue;

                    ChangeOwnerOfSettlementAction.ApplyByDefault(receiver, r.Settlement);
                    restored++;
                    Log.Info("InternalWar", war.Kingdom.Name + ": restitution - " + r.Settlement.Name + " passes from "
                                            + r.Holder.Name + " to " + r.ReturnsTo.Name
                                            + (r.ReturnsTo == r.HeldAtStart
                                                ? ", who held it when the war began."
                                                : " (the crown): " + (r.HeldAtStart?.Name?.ToString() ?? "nobody known")
                                                  + " held it when the war began and is not a house of the realm now."));
                }
                catch (Exception ex)
                {
                    Log.Error("InternalWar", "Restoring " + r.Settlement?.Name + " to " + r.ReturnsTo?.Name + " failed.", ex);
                }
            }

            Log.Info("InternalWar", war.Kingdom.Name + ": " + restored + " fief(s) restored after the crown's win ("
                                    + (war.Captures?.Count ?? 0) + " fief(s) taken across the line during the war).");
            if (restored > 0)
                Log.Notify(restored + " fief(s) the rebels took in " + war.Kingdom.Name + " have been restored.", Colors.Cyan);
            return restored;
        }
    }
}
