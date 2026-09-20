using System;
using System.Collections.Generic;
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
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        /// <summary>
        /// Samples every kingdom's smoothed strength. Here rather than in a system behavior
        /// because it belongs to no one pillar: greed, dread and coalitions all read it.
        ///
        /// Readers may see today's sample or yesterday's depending on which daily listener
        /// the engine calls first - and listeners do not fire in registration order. With an
        /// 84-day average, one day's difference moves nothing a decision reads.
        /// </summary>
        private void OnDailyTick()
        {
            if (_state == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                Diplomacy.Power.DailySample(_state);
            }
            catch (Exception ex)
            {
                Log.Error("Core", "Daily strength sample failed.", ex);
            }
        }

        /// <summary>
        /// A kingdom was destroyed - for an AI kingdom, the engine does this the moment its last
        /// settlement changes hands (FactionDiscontinuationCampaignBehavior, verified by IL in
        /// v1.4.8), so conquest now ends kingdoms.
        ///
        /// The engine removes the kingdom from every war through FactionManager and raises no
        /// peace event, and our war ledger closes wars only on the peace event. Without this,
        /// the conqueror would carry a war against a kingdom that no longer exists for the rest
        /// of the campaign - counted as a chosen war, so its own evaluation could never start
        /// another. No run has eliminated a kingdom yet, which is the only reason that never
        /// showed.
        /// </summary>
        private void OnKingdomDestroyed(Kingdom destroyed)
        {
            if (_state == null || destroyed == null) return;

            try
            {
                // Collected first: closing a war and releasing followers both touch the ledger.
                var wars = new List<WarRecord>();
                foreach (var war in _state.OngoingWarsOf(destroyed)) wars.Add(war);

                for (var i = 0; i < wars.Count; i++)
                {
                    var war = wars[i];
                    var enemy = war.Other(destroyed);

                    var previous = Telemetry.NotePeaceCause(Telemetry.PeaceCause.Eliminated,
                        "eliminated_" + destroyed.Name.ToString().Replace(' ', '_'));
                    try
                    {
                        CloseWar(war, destroyed, enemy);
                    }
                    finally
                    {
                        Telemetry.RestorePeaceCause(previous);
                    }

                    // Kingdoms fighting only because the destroyed one called them in have
                    // nothing left to fight for - the same release a peace would have given.
                    if (enemy != null && !enemy.IsEliminated)
                        Diplomacy.CallToArms.ReleaseFollowers(_state, destroyed, enemy);
                }

                // Its vassalages collapse the way a destroyed patron's always did, and nothing
                // else it signed can bind anyone now.
                Diplomacy.Hegemony.OnKingdomDestroyed(_state, destroyed);
                var remaining = new List<Treaty>();
                foreach (var treaty in _state.ActiveTreatiesOf(destroyed)) remaining.Add(treaty);
                for (var i = 0; i < remaining.Count; i++)
                    Diplomacy.TreatyRegistry.Dissolve(_state, remaining[i]);

                Telemetry.Event("kingdom_eliminated", "kingdom", destroyed, "warsClosed", wars.Count,
                    "treatiesDissolved", remaining.Count);
                Log.Info("Core", destroyed.Name + " no longer exists: " + wars.Count + " war(s) closed, "
                                 + remaining.Count + " other treaty(ies) dissolved.");
            }
            catch (Exception ex)
            {
                Log.Error("Core", "Handling the destruction of a kingdom failed.", ex);
            }
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

                var unchained = Hegemony.DissolveChains(_state);
                if (unchained > 0)
                    Log.Info("Hegemony", "Released " + unchained + " vassal(s) held by a kingdom that is itself a vassal.");

                var settled = Hegemony.SettleBreachesPredatingOaths(_state);
                if (settled > 0)
                    Log.Info("Claims", "Settled " + settled + " broken-treaty claim(s) that a later submission had already answered.");

                Log.Info("Core", "Session launched.");

                // Which build and which numbers produced this log, then a baseline of the world
                // as it loaded - so a run's first week has something to be compared with.
                Telemetry.WriteRunHeader(_state);
                if (Settings.Current.EnableTelemetry) Telemetry.WriteSnapshot(_state);
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
                Telemetry.Event("war_opened", "aggressor", a, "defender", d, "detail", detail,
                    "casusBelli", cb, "legitimacy", CasusBelli.Legitimacy(cb));
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

                CloseWar(war, a, b);
            }
            catch (Exception ex)
            {
                Log.Error("Core", "OnPeaceMade failed.", ex);
            }
        }

        /// <summary>The one way a war record closes, whether by peace or by elimination.</summary>
        private void CloseWar(WarRecord war, Kingdom a, Kingdom b)
        {
            war.Close();

            // The peace has to leave a mark, or a kingdom can walk straight into the
            // next war with nothing to show for the last one.
            Diplomacy.WarExhaustion.CarryOverToWeariness(_state, war);

            // Machine-readable first, so a long run can be parsed out of the log.
            if (Settings.Current.EnableTelemetry) Telemetry.WriteWarEnded(war);

            Log.Info("Core", "War closed after " + war.DaysElapsed.ToString("0") + " days: " + war
                             + " | weariness now " + a.Name + "=" + _state.WearinessOf(a).ToString("0.0")
                             + ", " + (b == null ? "?" : b.Name + "=" + _state.WearinessOf(b).ToString("0.0")) + ".");
        }

    }
}
