using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Behaviors
{
    /// <summary>
    /// Runs treaty and trust upkeep, and records the trust consequences of war and peace.
    ///
    /// Treaty expiry used to live in <see cref="CoreBehavior"/>; it moved here so that
    /// every ending of a treaty - expiry, breach, dissolution - goes through
    /// <see cref="TreatyRegistry"/> and cannot skip its reputational effect.
    /// </summary>
    public sealed class TreatyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnPeaceMade);
        }

        // Treaties and trust records live in ModState, owned by CoreBehavior.
        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyTick()
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                // Before the expiry sweep on purpose: a vassal with high hold renews its
                // term in place, and that decision has to happen before the registry retires
                // the treaty for having run out.
                Hegemony.DailyTick(state);

                TreatyRegistry.ExpireAndReward(state);
                TreatyRegistry.PayDueTribute(state);
                TreatyRegistry.PayPeaceDividends(state);
                TrustRegistry.DailyTick(state);
            }
            catch (Exception ex)
            {
                Log.Error("Treaty", "Daily treaty upkeep failed.", ex);
            }
        }

        /// <summary>
        /// One telemetry line per week. Left on by default: it costs a single log line and
        /// it is the only way a multi-hour balance run produces a dataset without somebody
        /// sitting in front of the screen.
        /// </summary>
        private void OnWeeklyTick()
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableTelemetry) return;

            try
            {
                Telemetry.WriteSnapshot(state);
            }
            catch (Exception ex)
            {
                Log.Error("Telemetry", "Weekly snapshot failed.", ex);
            }
        }

        /// <summary>
        /// A war that starts while a treaty forbade it means the treaty was broken to get
        /// there. The enforcement patches stop the AI from doing this by accident, so if we
        /// see it here it was either deliberate or came through an engine path we leave
        /// alone - and either way the agreement is over and should be recorded as breached.
        /// </summary>
        private void OnWarDeclared(IFaction attacker, IFaction defender, DeclareWarAction.DeclareWarDetail detail)
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                var aggressor = attacker as Kingdom;
                var target = defender as Kingdom;
                if (aggressor == null || target == null) return;
                if (Intrigue.InternalWars.IsFaction(aggressor) || Intrigue.InternalWars.IsFaction(target)) return;   // no treaty binds a rising

                var blocking = TreatyEnforcement.FirstBlockingTreaty(state, aggressor, target);
                if (blocking != null)
                {
                    Log.Info("Treaty", aggressor.Name + " went to war despite a " + blocking.Type
                                       + " with " + target.Name + " - recording it as a breach.");
                    TreatyRegistry.Break(state, blocking, aggressor);
                }

                // An unjustified war is noted by every court that is not in it - but it
                // has to be the legitimacy of the war actually being fought. Reading only
                // our claims here once branded a kingdom that honoured its vassalage as an
                // unjust aggressor, because answering a call to arms is not a claim.
                var legitimacy = CasusBelli.ResolvedLegitimacy(state, aggressor, target, detail);
                TrustRegistry.OnWarDeclared(state, aggressor, target, legitimacy);
            }
            catch (Exception ex)
            {
                Log.Error("Treaty", "War declaration trust handling failed.", ex);
            }
        }

        /// <summary>
        /// Peace ends the war and starts the clock on the truce. The truce is what stops the
        /// two sides re-declaring the next day, which is the single worst habit of the
        /// vanilla AI.
        /// </summary>
        private void OnPeaceMade(IFaction side1, IFaction side2, MakePeaceAction.MakePeaceDetail detail)
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                var a = side1 as Kingdom;
                var b = side2 as Kingdom;
                if (a == null || b == null) return;
                if (Intrigue.InternalWars.IsFaction(a) || Intrigue.InternalWars.IsFaction(b)) return;

                // Anyone dragged in by either side is let out with them.
                CallToArms.ReleaseFollowers(state, a, b);
                CallToArms.ReleaseFollowers(state, b, a);

                if (state.ActiveTreatyBetween(a, b, Models.TreatyType.Truce) != null) return;

                // Signed directly rather than through Sign(), which requires an active war:
                // by the time this event fires the war is already over.
                var truce = TreatyRegistry.SignTruceOnPeace(state, a, b);
                if (truce != null)
                    Log.Info("Treaty", "Truce recorded until " + truce.ExpiresOn + " between "
                                       + a.Name + " and " + b.Name + ".");
            }
            catch (Exception ex)
            {
                Log.Error("Treaty", "Truce creation on peace failed.", ex);
            }
        }
    }
}
