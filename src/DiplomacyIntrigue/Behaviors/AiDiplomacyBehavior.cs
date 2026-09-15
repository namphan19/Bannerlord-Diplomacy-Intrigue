using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Behaviors
{
    /// <summary>
    /// Gives every AI kingdom one diplomatic evaluation per week.
    ///
    /// The work is spread by day rather than done in a weekly burst: each kingdom gets a
    /// slot based on its index, so a daily tick evaluates one or two realms instead of
    /// twelve. That keeps the tick cheap and, more importantly, staggers the AI's moves so
    /// they arrive through the month rather than all at once every seventh day.
    ///
    /// The player's own kingdom is never evaluated. Their foreign policy is theirs.
    /// </summary>
    public sealed class AiDiplomacyBehavior : CampaignBehaviorBase
    {
        private const int SlotCount = 7;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyTick()
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                var slot = (int)(CampaignTime.Now.ToDays % SlotCount);
                var index = 0;

                foreach (var kingdom in Kingdom.All)
                {
                    var mySlot = index++ % SlotCount;
                    if (mySlot != slot) continue;

                    if (kingdom.IsEliminated) continue;
                    if (IsPlayerRuled(kingdom)) continue;

                    var move = AiDiplomacy.Evaluate(state, kingdom);

                    // "Nothing" is the expected answer most weeks, so it is only logged
                    // when verbose - otherwise it would bury the moves that matter.
                    if (move == AiDiplomacy.Move.None)
                    {
                        if (Settings.Current.VerboseLogging)
                            Log.Debug("AI", kingdom.Name + " made no diplomatic move this week.");
                        continue;
                    }

                    Log.Debug("AI", kingdom.Name + " weekly move: " + move + ".");
                }
            }
            catch (Exception ex)
            {
                Log.Error("AI", "Weekly diplomacy evaluation failed.", ex);
            }
        }

        private static bool IsPlayerRuled(Kingdom kingdom)
            => Hero.MainHero != null && kingdom.Leader == Hero.MainHero;
    }
}
