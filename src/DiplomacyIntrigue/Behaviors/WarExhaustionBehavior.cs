using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Behaviors
{
    /// <summary>
    /// Wires campaign events into <see cref="WarExhaustion"/>. All state lives in
    /// <see cref="ModState"/>, owned by <see cref="CoreBehavior"/>, so this behavior
    /// stores nothing of its own and has no save data.
    ///
    /// Every handler is wrapped: an exception escaping a campaign event handler takes the
    /// game down, and a mis-attributed battle is not worth that.
    /// </summary>
    public sealed class WarExhaustionBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.VillageLooted.AddNonSerializedListener(this, OnVillageLooted);
        }

        // No own data: exhaustion is a field of the war records in ModState.
        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyTick()
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                WarExhaustion.DailyTick(state);
            }
            catch (Exception ex)
            {
                Log.Error("Exhaustion", "Daily accrual failed.", ex);
            }
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            var state = CoreBehavior.State;
            if (state == null || mapEvent == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                WarExhaustion.ApplyBattleResult(state, mapEvent);
            }
            catch (Exception ex)
            {
                Log.Error("Exhaustion", "Battle result handling failed.", ex);
            }
        }

        /// <summary>
        /// Fires for every ownership change, including inheritance, gifts and kingdom
        /// grants. Only a transfer between two kingdoms at war is a conquest, and
        /// <see cref="WarExhaustion.ApplyFiefCapture"/> filters on exactly that.
        /// </summary>
        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner,
            Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            var state = CoreBehavior.State;
            if (state == null || settlement == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                if (detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege &&
                    detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByRebellion)
                    return;

                var captor = newOwner?.Clan?.Kingdom;
                var formerOwner = oldOwner?.Clan?.Kingdom;
                WarExhaustion.ApplyFiefCapture(state, settlement, captor, formerOwner);
            }
            catch (Exception ex)
            {
                Log.Error("Exhaustion", "Fief capture handling failed.", ex);
            }
        }

        private void OnVillageLooted(Village village)
        {
            var state = CoreBehavior.State;
            if (state == null || village == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                // The raider is whoever currently holds the siege/raid event on the village;
                // by the time looting completes the party has usually moved on, so read the
                // last besieger camp if present and fall back to the map event.
                var raider = village.Settlement?.LastAttackerParty?.MapFaction as Kingdom;
                if (raider == null) return;

                WarExhaustion.ApplyVillageRaided(state, village, raider);
            }
            catch (Exception ex)
            {
                Log.Error("Exhaustion", "Village raid handling failed.", ex);
            }
        }
    }
}
