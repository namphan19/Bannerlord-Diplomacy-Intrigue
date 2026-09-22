using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Intrigue;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Behaviors
{
    /// <summary>
    /// Drives the court-intrigue pillar's upkeep (Phase 2).
    ///
    /// Separate from <see cref="TreatyBehavior"/> rather than folded into it, because the two
    /// pillars have independent settings toggles: a player who turns court intrigue off must
    /// keep a working diplomacy, and a daily handler that returns early on
    /// <c>EnableDiplomacy</c> would silently freeze every grievance in the world.
    ///
    /// The same upkeep is driven by <c>DebugCommands.RunDailyUpkeep</c>, so
    /// <c>diplomacy.tick_days</c> and <c>diplomacy.ai_week</c> age grievances exactly as the
    /// campaign does. That is deliberate and it is the rule CLAUDE.md §1 records: a
    /// diagnostic that drives only part of a tick lies convincingly, and a value that looks
    /// frozen under a debug command has already cost this project a day once.
    /// </summary>
    public sealed class IntrigueBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        // Grievances live in ModState, owned by CoreBehavior.
        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyTick()
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableIntrigue) return;

            // No throw crosses the engine boundary: a campaign event handler that throws
            // takes the whole game down, not just the mod (CLAUDE.md §3).
            try
            {
                GrievanceRegistry.DailyTick(state);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Daily intrigue upkeep failed.", ex);
            }
        }
    }
}
