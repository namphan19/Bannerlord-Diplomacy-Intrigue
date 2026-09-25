using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Espionage;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Behaviors
{
    /// <summary>
    /// Drives the espionage pillar's upkeep (Phase 3).
    ///
    /// Its own behaviour, under its own settings toggle, for the reason <see cref="IntrigueBehavior"/>
    /// gives: a player who turns espionage off must keep working diplomacy and a working court,
    /// and one handler returning early on another pillar's switch would freeze this one.
    ///
    /// The same upkeep is driven by <c>DebugCommands.RunDailyUpkeep</c> and <c>ai_week</c>, so the
    /// debug commands wear networks exactly as the campaign does (CLAUDE.md §1).
    /// </summary>
    public sealed class EspionageBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
        }

        // Networks live in ModState, owned by CoreBehavior.
        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyTick()
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableEspionage) return;

            // No throw crosses the engine boundary (CLAUDE.md §3).
            try
            {
                SpyNetworks.DailyTick(state);
            }
            catch (Exception ex)
            {
                Log.Error("Espionage", "Daily espionage upkeep failed.", ex);
            }

            // Its own try: an operation that fails to resolve must not be read as a failed upkeep.
            try
            {
                Missions.DailyTick(state);
            }
            catch (Exception ex)
            {
                Log.Error("Espionage", "Resolving spy missions failed.", ex);
            }
        }

        private void OnWeeklyTick()
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableEspionage) return;

            try
            {
                SpyNetworks.WeeklyTick(state);
            }
            catch (Exception ex)
            {
                Log.Error("Espionage", "Weekly espionage upkeep failed.", ex);
            }
        }
    }
}
