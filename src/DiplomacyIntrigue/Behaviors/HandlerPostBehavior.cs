using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Espionage;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Behaviors
{
    /// <summary>
    /// Two engine vetoes that keep a posted handler on their post (story 3.8). Nothing here
    /// decides anything: each listener answers one question vanilla already asks, and the
    /// answer is read from <see cref="SpyNetworks.PostedNetwork"/> - the one gate, so the pillar's
    /// switch is honoured by both or by neither (R7).
    ///
    /// WHY EVENTS AND NOT HARMONY: these are the questions the engine asks, and a campaign event
    /// is where an answer belongs. It also reaches the player: <c>Hero.CanLeadParty</c> is what
    /// the clan screen's party-leader list filters on, so a handler in the player's own clan
    /// cannot be given a party either - the same rule, not an AI exemption (R2).
    ///
    /// WHAT EACH ONE CLOSES, from run 09 (the 21 handler losses in two campaign years, 16 of
    /// them a party):
    ///
    ///   <see cref="OnCanHeroLeadParty"/> - R2, the party. No <c>GameModel</c> in v1.4.8 answers
    ///   "may this hero lead a party", so there was nothing to override: the hero was handed a
    ///   party, <c>SpyNetworks.StillHandles</c> released them the next day, and the network sat
    ///   idle until the AI's weekly plan found another candidate.
    ///
    ///   <see cref="OnCanMoveToSettlement"/> - R4, the station. Vanilla moves idle AI lords home;
    ///   run 09 found 7 of 7 AI handlers outside their target on day 15 while their networks kept
    ///   growing, because nothing checked.
    ///
    /// WHAT THIS DOES NOT REACH: vanilla's second pass for a clan with no other free lord
    /// ignores this veto (from IL, v1.5.3). Story 3.8 left that path measured, not patched (D2);
    /// run 11 measured it at 3.0 a year (`forced-party`), and on 2026-10-02 the lead chose
    /// Harmony for it: <c>HeroSpawn_GetBestAvailableCommander_Patch</c>. The daily check still
    /// names a <c>forced-party</c> loss if one ever gets through, so the patch is checked by the
    /// same count that justified it.
    ///
    /// VERIFIED AGAINST: the two event signatures read on the v1.4.8 reference assemblies
    /// (BUTR 1.4.8.119303) and on the v1.5.3 install, identical on both - see story 3.8 ST-1.
    ///
    /// FAILURE MODE: each listener catches and leaves the question as vanilla answered it, so a
    /// fault here costs a handler, not the game.
    /// </summary>
    public sealed class HandlerPostBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.CanHeroLeadPartyEvent.AddNonSerializedListener(this, OnCanHeroLeadParty);
            CampaignEvents.CanMoveToSettlementEvent.AddNonSerializedListener(this, OnCanMoveToSettlement);
        }

        // Both answers are read from ModState, owned by CoreBehavior; nothing here is saved.
        public override void SyncData(IDataStore dataStore) { }

        /// <summary>
        /// A hero who runs a network cannot be given a party to lead (story 3.8 R2).
        ///
        /// Asked by vanilla for the AI's army and caravan models, by the clan screen's party
        /// lists, and by the settlement menu's "send members" - so one listener answers for the
        /// AI's week and for the player's click, which is the same rule either way.
        /// </summary>
        private void OnCanHeroLeadParty(Hero hero, ref bool result)
        {
            // Only ever a veto: another listener may already have said no, and a handler is a
            // reason not to, never a reason to.
            if (!result) return;

            try
            {
                if (SpyNetworks.PostedNetwork(hero) == null) return;
                result = false;
                // Recorded so the daily check can tell this residual apart from a post that ended
                // with a party on some path that never asked (story 3.8 R5). Read once, then
                // forgotten; a static, never save data.
                SpyNetworks.MarkPartyVetoRefused(hero);
                Log.Debug("Espionage", hero.Name + " runs a spy network and cannot be given a party.");
            }
            catch (Exception ex)
            {
                Log.Error("Espionage", "Refusing a party for " + hero?.Name + " failed; vanilla decides.", ex);
            }
        }

        /// <summary>
        /// A handler may move between settlements of the realm they are posted in, and not out of
        /// it (story 3.8 R4).
        ///
        /// The event carries the hero and the answer, not the destination, so the question is
        /// answered from where the hero is: inside the target realm, any move within it is the
        /// post's own business; outside it, every move vanilla offers is a step away from the
        /// station. That is the direction this refuses, and the daily check in
        /// <see cref="SpyNetworks"/> fetches back a handler who is abroad for any other reason
        /// (a battle, a script), so nothing depends on this being the only path.
        /// </summary>
        private void OnCanMoveToSettlement(Hero hero, ref bool result)
        {
            if (!result) return;

            try
            {
                var network = SpyNetworks.PostedNetwork(hero);
                if (network == null) return;
                if (SpyNetworks.InTargetRealm(hero, network.Target)) return;

                result = false;
                Log.Debug("Espionage", hero.Name + " is posted in " + network.Target.Name
                                        + " and cannot leave it: " + (hero.CurrentSettlement?.Name?.ToString() ?? "the road")
                                        + " is outside it.");
            }
            catch (Exception ex)
            {
                Log.Error("Espionage", "Refusing a move for " + hero?.Name + " failed; vanilla decides.", ex);
            }
        }
    }
}
