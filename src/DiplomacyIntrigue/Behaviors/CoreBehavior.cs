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
    /// Owns the campaign-scoped Diplomacy &amp; Intrigue state and keeps the war ledger in
    /// step with the base game. Every other system reads its data through here.
    ///
    /// Note on ordering: campaign event listeners do NOT fire in registration order - a
    /// live trace showed CallToArmsBehavior handling WarDeclared before this behavior did,
    /// for the same event. Nothing depends on the order: the state object exists from
    /// construction, and every reader tolerates a war record that has not been opened yet.
    /// Do not add anything that assumes otherwise.
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

                // One resolver for everyone - see CasusBelli.Resolve for why.
                var cb = CasusBelli.Resolve(_state, a, d, detail);

                _state.Wars.Add(new WarRecord(a, d, cb));
                Log.Info("Core", "War opened: " + a.Name + " -> " + d.Name + " (" + detail + " => " + cb
                                 + ", legitimacy " + CasusBelli.Legitimacy(cb).ToString("0.00") + ").");
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

                // Machine-readable first, so a long run can be parsed out of the log.
                if (Settings.Current.EnableTelemetry) Telemetry.WriteWarEnded(war);

                Log.Info("Core", "War closed after " + war.DaysElapsed.ToString("0") + " days: " + war
                                 + " | weariness now " + a.Name + "=" + _state.WearinessOf(a).ToString("0.0")
                                 + ", " + b.Name + "=" + _state.WearinessOf(b).ToString("0.0") + ".");
            }
            catch (Exception ex)
            {
                Log.Error("Core", "OnPeaceMade failed.", ex);
            }
        }

    }
}
