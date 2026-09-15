using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// Keeps the record of who held which fief, and when.
    ///
    /// The base game does not expose settlement ownership history, so without this there
    /// is no way to ask "did we hold Pravend within living memory?" - and therefore no way
    /// to have historical claims at all.
    ///
    /// Honest limitation: a campaign that existed before this mod was installed has no
    /// history to read. Seeding records the *current* holders as of install, so ancestral
    /// claims simply are not available on day one and become available as the map changes.
    /// </summary>
    public static class FiefHistory
    {
        /// <summary>
        /// Opens a record for every fortification's current owner, once. Safe to call on
        /// every session launch: it only fills in fiefs that have no open record yet, so a
        /// newly conquered or newly created settlement gets picked up too.
        /// </summary>
        public static int Seed(ModState state)
        {
            var added = 0;
            foreach (var settlement in Settlement.All)
            {
                if (!settlement.IsFortification) continue;

                var kingdom = settlement.MapFaction as Kingdom;
                if (kingdom == null) continue;
                if (CurrentRecord(state, settlement) != null) continue;

                state.FiefHistory.Add(new FiefOwnershipRecord(settlement, kingdom, CampaignTime.Now));
                added++;
            }
            return added;
        }

        /// <summary>
        /// Closes the open record for the settlement and opens one for the new holder.
        /// Called for every ownership change, including peaceful ones - a fief granted away
        /// and later reclaimed is just as much a historical claim as a conquered one.
        /// </summary>
        public static void RecordTransfer(ModState state, Settlement settlement, Kingdom newHolder)
        {
            if (settlement == null) return;

            var open = CurrentRecord(state, settlement);
            if (open != null)
            {
                // A grant between clans of the same kingdom does not change the holder.
                if (open.Kingdom == newHolder) return;
                open.Close(CampaignTime.Now);
            }

            if (newHolder != null)
                state.FiefHistory.Add(new FiefOwnershipRecord(settlement, newHolder, CampaignTime.Now));
        }

        public static FiefOwnershipRecord CurrentRecord(ModState state, Settlement settlement)
        {
            for (var i = 0; i < state.FiefHistory.Count; i++)
            {
                var record = state.FiefHistory[i];
                if (record.IsCurrent && record.Settlement == settlement) return record;
            }
            return null;
        }

        /// <summary>
        /// True when the kingdom held this fief recently enough to claim it back. Current
        /// ownership does not count - you cannot claim what you already hold.
        /// </summary>
        public static bool HeldWithinMemory(ModState state, Kingdom kingdom, Settlement settlement)
        {
            if (kingdom == null || settlement == null) return false;
            if (settlement.MapFaction == kingdom) return false;

            for (var i = 0; i < state.FiefHistory.Count; i++)
            {
                var record = state.FiefHistory[i];
                if (record.Settlement != settlement || record.Kingdom != kingdom) continue;
                if (record.IsCurrent) continue;
                if (record.YearsSinceLost <= DiplomacyConstants.AncestralClaimMemoryYears) return true;
            }
            return false;
        }

        /// <summary>
        /// Every fief the kingdom lost within memory and does not hold now - the raw
        /// material for its ancestral claims.
        /// </summary>
        public static void CollectLostFiefs(ModState state, Kingdom kingdom, System.Collections.Generic.List<Settlement> into)
        {
            into.Clear();
            if (kingdom == null) return;

            for (var i = 0; i < state.FiefHistory.Count; i++)
            {
                var record = state.FiefHistory[i];
                if (record.Kingdom != kingdom || record.IsCurrent) continue;
                if (record.YearsSinceLost > DiplomacyConstants.AncestralClaimMemoryYears) continue;

                var settlement = record.Settlement;
                if (settlement == null || settlement.MapFaction == kingdom) continue;
                if (!into.Contains(settlement)) into.Add(settlement);
            }
        }
    }
}
