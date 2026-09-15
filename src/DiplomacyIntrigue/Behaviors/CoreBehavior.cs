using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Behaviors
{
    /// <summary>
    /// Owns the campaign-scoped Diplomacy & Intrigue state and keeps the war ledger in step with
    /// the base game. Every other Diplomacy & Intrigue system reads its data through here, so this
    /// behavior must be registered first.
    /// </summary>
    public sealed class CoreBehavior : CampaignBehaviorBase
    {
        private ModState _state = new ModState();

        public static CoreBehavior Current
            => Campaign.Current == null ? null : Campaign.Current.GetCampaignBehavior<CoreBehavior>();

        /// <summary>Null outside a campaign. Callers must handle that.</summary>
        public static ModState State
        {
            get
            {
                var behavior = Current;
                return behavior == null ? null : behavior._state;
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnPeaceMade);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("ModState", ref _state);

            if (!dataStore.IsLoading) return;

            if (_state == null)
            {
                Log.Info("Core", "No Diplomacy & Intrigue data in this save - starting fresh.");
                _state = new ModState();
            }
            else
            {
                _state.AfterLoad();
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try
            {
                BackfillOngoingWars();
                Log.Info("Core", "Session launched.");
            }
            catch (Exception ex)
            {
                Log.Error("Core", "OnSessionLaunched failed.", ex);
            }
        }

        /// <summary>
        /// Diplomacy & Intrigue can be added to a campaign that is already running, where wars
        /// started before the mod ever loaded. Open a record for every war we do not
        /// know about yet so the other systems have something to work with.
        /// </summary>
        private void BackfillOngoingWars()
        {
            var opened = 0;
            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom.IsEliminated) continue;
                foreach (var other in Kingdom.All)
                {
                    if (other == kingdom || other.IsEliminated) continue;
                    if (!kingdom.IsAtWarWith(other)) continue;
                    if (_state.OngoingWarBetween(kingdom, other) != null) continue;

                    // There is no way to know who started it, so record it unattributed
                    // and let the exhaustion model treat both sides symmetrically.
                    _state.Wars.Add(new WarRecord(kingdom, other, CasusBelliType.None));
                    opened++;
                }
            }
            if (opened > 0) Log.Info("Core", "Backfilled " + opened + " pre-existing war(s).");
        }

        private void OnWarDeclared(IFaction attacker, IFaction defender, DeclareWarAction.DeclareWarDetail detail)
        {
            try
            {
                var a = attacker as Kingdom;
                var d = defender as Kingdom;
                if (a == null || d == null) return;   // minor factions are out of scope for now
                if (_state.OngoingWarBetween(a, d) != null) return;

                var cb = CasusBelli.FromDeclareWarDetail(detail);
                _state.Wars.Add(new WarRecord(a, d, cb));
                Log.Info("Core", "War opened: " + a.Name + " -> " + d.Name + " (" + detail + " => " + cb + ").");
            }
            catch (Exception ex)
            {
                Log.Error("Core", "OnWarDeclared failed.", ex);
            }
        }

        private void OnPeaceMade(IFaction side1, IFaction side2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                var a = side1 as Kingdom;
                var b = side2 as Kingdom;
                if (a == null || b == null) return;

                var war = _state.OngoingWarBetween(a, b);
                if (war == null) return;

                war.Close();

                // The peace has to leave a mark, or a kingdom can walk straight into the
                // next war with nothing to show for the last one.
                Diplomacy.WarExhaustion.CarryOverToWeariness(_state, war);

                Log.Info("Core", "War closed after " + war.DaysElapsed.ToString("0") + " days: " + war
                                 + " | weariness now " + a.Name + "=" + _state.WearinessOf(a).ToString("0.0")
                                 + ", " + b.Name + "=" + _state.WearinessOf(b).ToString("0.0") + ".");
            }
            catch (Exception ex)
            {
                Log.Error("Core", "OnPeaceMade failed.", ex);
            }
        }

        private void OnDailyTick()
        {
            try
            {
                ExpireTreaties();
            }
            catch (Exception ex)
            {
                Log.Error("Core", "Daily tick failed.", ex);
            }
        }

        private void ExpireTreaties()
        {
            for (var i = 0; i < _state.Treaties.Count; i++)
            {
                var treaty = _state.Treaties[i];
                if (!treaty.IsActive || !treaty.HasRunOut) continue;

                treaty.Close(TreatyStatus.Expired);
                Log.Info("Core", "Treaty expired: " + treaty + ".");
            }
        }
    }
}
