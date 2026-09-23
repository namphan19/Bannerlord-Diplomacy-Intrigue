using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Behaviors
{
    /// <summary>
    /// Maintains the fief ownership ledger and the casus belli that come out of it.
    ///
    /// Wars in this mod are declared *for* something, and this is where the "something"
    /// accumulates: fiefs lost become claims to retake them, raids become claims to
    /// retaliate, and both fade if nobody acts on them.
    /// </summary>
    public sealed class ClaimsBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.VillageLooted.AddNonSerializedListener(this, OnVillageLooted);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        // All data lives in ModState, owned by CoreBehavior.
        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            var state = CoreBehavior.State;
            if (state == null) return;

            try
            {
                var seeded = FiefHistory.Seed(state);
                if (seeded > 0)
                    Log.Info("Claims", "Fief ledger seeded with " + seeded + " holding(s).");
            }
            catch (Exception ex)
            {
                Log.Error("Claims", "Seeding the fief ledger failed.", ex);
            }
        }

        /// <summary>
        /// Records the transfer, and gives the loser a claim when the fief was taken by
        /// force. A fief handed over by barter, gift or a kingdom vote is still recorded in
        /// the ledger - it can be claimed back a generation later - but it does not hand
        /// out an immediate casus belli, because nothing was actually wronged.
        /// </summary>
        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner,
            Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            var state = CoreBehavior.State;
            if (state == null || settlement == null) return;

            try
            {
                var formerHolder = oldOwner?.Clan?.Kingdom;
                var newHolder = newOwner?.Clan?.Kingdom;

                FiefHistory.RecordTransfer(state, settlement, newHolder);

                if (!Settings.Current.EnableDiplomacy) return;
                if (detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;
                if (formerHolder == null || newHolder == null || formerHolder == newHolder) return;

                var claim = ClaimRegistry.GrantAncestralClaim(state, formerHolder, settlement);
                if (claim != null)
                    Log.Info("Claims", formerHolder.Name + " now has a claim to retake "
                                       + settlement.Name + " from " + newHolder.Name + ".");
            }
            catch (Exception ex)
            {
                Log.Error("Claims", "Ownership change handling failed.", ex);
            }
        }

        private void OnVillageLooted(Village village)
        {
            var state = CoreBehavior.State;
            if (state == null || village == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                var victim = village.Settlement?.MapFaction as Kingdom;
                var raider = village.Settlement?.LastAttackerParty?.MapFaction as Kingdom;
                if (victim == null || raider == null || victim == raider) return;
                if (Intrigue.InternalWars.IsFaction(victim) || Intrigue.InternalWars.IsFaction(raider)) return;   // a raid inside an internal war is not a casus belli

                ClaimRegistry.GrantRaidClaim(state, victim, raider);
            }
            catch (Exception ex)
            {
                Log.Error("Claims", "Raid claim handling failed.", ex);
            }
        }

        private void OnDailyTick()
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                var expired = ClaimRegistry.ExpireStale(state);
                if (expired > 0) Log.Debug("Claims", expired + " claim(s) expired.");

                ClaimRegistry.ResolveFabrications(state);
            }
            catch (Exception ex)
            {
                Log.Error("Claims", "Daily claim upkeep failed.", ex);
            }
        }
    }
}
