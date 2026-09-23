using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Behaviors
{
    /// <summary>
    /// Issues the call to arms when a war starts.
    ///
    /// Both sides get to call: the defender because it was attacked, the aggressor because
    /// an alliance obliges help in offensive wars too. Which obligations actually apply is
    /// <see cref="CallToArms"/>'s business - a defensive pact, for instance, never answers
    /// an aggressor.
    /// </summary>
    public sealed class CallToArmsBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        }

        // Obligations are read from the treaties in ModState; nothing to store here.
        public override void SyncData(IDataStore dataStore) { }

        private void OnWarDeclared(IFaction attacker, IFaction defender, DeclareWarAction.DeclareWarDetail detail)
        {
            var state = CoreBehavior.State;
            if (state == null || !Settings.Current.EnableDiplomacy) return;

            try
            {
                var aggressor = attacker as Kingdom;
                var target = defender as Kingdom;
                if (aggressor == null || target == null) return;
                if (Intrigue.InternalWars.IsFaction(aggressor) || Intrigue.InternalWars.IsFaction(target)) return;   // an internal war summons no allies (design 07 §3b)

                // The defender's allies are answering an attack; the aggressor's are being
                // asked to join a war of its choosing. The flag is what a defensive pact
                // keys off.
                CallToArms.Issue(state, target, aggressor, callerWasAttacked: true);
                CallToArms.Issue(state, aggressor, target, callerWasAttacked: false);
            }
            catch (Exception ex)
            {
                Log.Error("CallToArms", "Issuing the call to arms failed.", ex);
            }
        }
    }
}
