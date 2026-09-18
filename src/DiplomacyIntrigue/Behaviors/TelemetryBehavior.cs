using System;
using DiplomacyIntrigue.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Behaviors
{
    /// <summary>
    /// Records the engine events that shape the map but are not ours to decide: fiefs changing
    /// hands, clans changing sides, rulers dying and being replaced. And once an in-game year,
    /// a full report file.
    ///
    /// Written for balance runs nobody watches. The diplomacy systems log their own decisions;
    /// what they could not show was the ground moving underneath them - a kingdom that loses
    /// three clans to defection looks, in the strength column, exactly like one that lost a
    /// war. Records only; nothing here changes the campaign.
    /// </summary>
    public sealed class TelemetryBehavior : CampaignBehaviorBase
    {
        /// <summary>The campaign year the last report was written for. Session-scoped.</summary>
        private int _reportedYear = -1;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.RulingClanChanged.AddNonSerializedListener(this, OnRulingClanChanged);
            CampaignEvents.BeforeHeroKilledEvent.AddNonSerializedListener(this, OnBeforeHeroKilled);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private static bool Enabled => CoreBehavior.State != null && Settings.Current.EnableTelemetry;

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner,
            Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (!Enabled || settlement == null || !settlement.IsFortification) return;

            try
            {
                Telemetry.Event("fief_changed",
                    "settlement", settlement.Name,
                    "type", settlement.IsTown ? "town" : "castle",
                    "from", oldOwner?.Clan?.Kingdom,
                    "to", newOwner?.Clan?.Kingdom,
                    "fromClan", oldOwner?.Clan,
                    "toClan", newOwner?.Clan,
                    "detail", detail,
                    "capturer", capturerHero);
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "Fief change record failed.", ex);
            }
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            if (!Enabled || clan == null) return;

            try
            {
                var fiefs = 0;
                for (var i = 0; i < clan.Settlements.Count; i++)
                    if (clan.Settlements[i].IsFortification) fiefs++;

                Telemetry.Event("clan_changed_kingdom",
                    "clan", clan,
                    "from", oldKingdom,
                    "to", newKingdom,
                    "detail", detail,
                    "fiefs", fiefs,
                    "strength", clan.CurrentTotalStrength);
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "Clan change record failed.", ex);
            }
        }

        private void OnRulingClanChanged(Kingdom kingdom, Clan oldRulingClan)
        {
            if (!Enabled || kingdom == null) return;

            try
            {
                Telemetry.Event("ruler_changed",
                    "kingdom", kingdom,
                    "oldRulingClan", oldRulingClan,
                    "newRulingClan", kingdom.RulingClan,
                    "ruler", kingdom.Leader);
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "Ruler change record failed.", ex);
            }
        }

        private void OnBeforeHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail,
            bool showNotification)
        {
            if (!Enabled || victim == null) return;

            try
            {
                // Rulers only, and the player: a lord dying is ordinary, a ruler dying can
                // change a kingdom's whole foreign policy, and the player dying ends the run.
                if (!victim.IsKingdomLeader && victim != Hero.MainHero) return;

                Telemetry.Event(victim == Hero.MainHero ? "player_died" : "ruler_died",
                    "hero", victim,
                    "kingdom", victim.Clan?.Kingdom,
                    "age", victim.Age,
                    "detail", detail,
                    "killer", killer);
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "Death record failed.", ex);
            }
        }

        /// <summary>
        /// One full report file per campaign year, written on the first day the year changes.
        /// The log holds the timeline; the reports hold the whole world at yearly intervals, for
        /// questions nobody thought to log.
        /// </summary>
        private void OnDailyTick()
        {
            if (!Enabled) return;

            try
            {
                var year = CampaignTime.Now.GetYear;
                if (_reportedYear < 0) { _reportedYear = year; return; }
                if (year == _reportedYear) return;

                _reportedYear = year;
                var path = Telemetry.WriteReport(CoreBehavior.State);
                Telemetry.Event("yearly_report", "file", System.IO.Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "Yearly report failed.", ex);
            }
        }
    }
}
