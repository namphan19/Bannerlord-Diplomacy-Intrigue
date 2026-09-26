using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Intrigue;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

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
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.RulingClanChanged.AddNonSerializedListener(this, OnRulingClanChanged);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, OnMobilePartyCreated);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.OnClanLeaderChangedEvent.AddNonSerializedListener(this, OnClanLeaderChanged);
        }

        // Grievances live in ModState, owned by CoreBehavior.
        public override void SyncData(IDataStore dataStore) { }

        /// <summary>
        /// The bloc memo holds Kingdom references from whatever campaign was loaded before
        /// this one. Dropping them on session start keeps a stale court from being served to
        /// a fresh world.
        /// </summary>
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try
            {
                BlocModel.Reset();
                SuccessionModel.Reset();
                InternalWars.Reset();
                SideChange.Reset();
                InternalWars.RebuildIndex(CoreBehavior.State);

                // The engine never saves a kingdom's clan and fief lists, so a rising comes back
                // from a load with empty ones. Filled here, after the engine's own load-time
                // rebuild, which would otherwise start from scratch over the top of them.
                InternalWars.SyncAll(CoreBehavior.State);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Resetting the bloc cache failed.", ex);
            }
        }

        private void OnDailyTick()
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableIntrigue) return;

            // No throw crosses the engine boundary: a campaign event handler that throws
            // takes the whole game down, not just the mod (CLAUDE.md §3).
            try
            {
                GrievanceRegistry.DailyTick(state);
                LegitimacyRegistry.DailyTick(state);
                SuccessionModel.DailyWatch(state);
                SuccessionModel.RetireSpentClaims(state);
                InternalWars.DailyTick(state);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Daily intrigue upkeep failed.", ex);
            }
        }

        /// <summary>
        /// The court's week: the conditions that renew grievances, the civil wars' leaders buying
        /// houses, the AI rulers making amends. One list, <see cref="IntrigueUpkeep.Weekly"/>,
        /// shared with <c>diplomacy.ai_week</c>; it catches its own failures step by step.
        /// </summary>
        private void OnWeeklyTick()
        {
            try
            {
                IntrigueUpkeep.Weekly(CoreBehavior.State);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Weekly intrigue upkeep failed.", ex);
            }
        }

        /// <summary>
        /// The immediate half of succession detection. The daily watch in
        /// <see cref="SuccessionModel.DailyWatch"/> covers what this event does not see; the
        /// two cannot double-fire.
        /// </summary>
        private void OnRulingClanChanged(Kingdom kingdom, Clan oldRulingClan)
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableIntrigue) return;

            try
            {
                SuccessionModel.OnRulingClanChanged(state, kingdom, oldRulingClan);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Succession politics failed.", ex);
            }
        }

        private void OnWarDeclared(IFaction aggressor, IFaction defender,
            DeclareWarAction.DeclareWarDetail detail)
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableIntrigue) return;

            try
            {
                // A rising is not a realm; its declaration against the crown is not a war the
                // court judges (design 07 §3b).
                if (!(aggressor as Kingdom).IsRealm() || !(defender as Kingdom).IsRealm()) return;

                GrievanceSources.OnWarDeclared(state, aggressor as Kingdom, defender as Kingdom, detail);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Grievance on war declaration failed.", ex);
            }
        }

        /// <summary>A battle between the two sides of an internal war wears them down.</summary>
        private void OnMapEventEnded(TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent)
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableIntrigue) return;

            try
            {
                InternalWars.OnMapEventEnded(state, mapEvent);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Internal war battle accounting failed.", ex);
            }
        }

        private void OnMobilePartyCreated(TaleWorlds.CampaignSystem.Party.MobileParty party)
        {
            if (!InternalWars.Any || party?.WarPartyComponent == null) return;
            try
            {
                InternalWars.OnRosterChanged(CoreBehavior.State, party.ActualClan);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Internal war roster update (party raised) failed.", ex);
            }
        }

        private void OnMobilePartyDestroyed(TaleWorlds.CampaignSystem.Party.MobileParty party,
            TaleWorlds.CampaignSystem.Party.PartyBase destroyer)
        {
            if (!InternalWars.Any || party?.WarPartyComponent == null) return;
            try
            {
                // The clan may already be detached from a party being finalised, so every
                // running rising is re-synced rather than the one the clan would name.
                InternalWars.OnRosterChanged(CoreBehavior.State, null);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Internal war roster update (party destroyed) failed.", ex);
            }
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail,
            bool showNotification)
        {
            if (!InternalWars.Any || victim?.Clan == null) return;
            try
            {
                InternalWars.OnRosterChanged(CoreBehavior.State, victim.Clan, victim);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Internal war roster update (hero killed) failed.", ex);
            }
        }

        /// <summary>A house's head changed; if by death and contested, it may divide (2.6b).</summary>
        private void OnClanLeaderChanged(Hero oldLeader, Hero newLeader)
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableIntrigue) return;

            try
            {
                ClanSuccession.OnClanLeaderChanged(state, oldLeader, newLeader);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Clan succession politics failed.", ex);
            }
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableIntrigue) return;

            try
            {
                InternalWars.OnClanChangedKingdom(state, clan, oldKingdom);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Internal war membership update failed.", ex);
            }
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner,
            Hero oldOwner, Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableIntrigue) return;

            try
            {
                InternalWars.OnSettlementOwnerChanged(state, settlement, newOwner, oldOwner);
                GrievanceSources.OnSettlementOwnerChanged(state, settlement, newOwner, oldOwner, detail);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Grievance on settlement owner change failed.", ex);
            }
        }
    }
}
