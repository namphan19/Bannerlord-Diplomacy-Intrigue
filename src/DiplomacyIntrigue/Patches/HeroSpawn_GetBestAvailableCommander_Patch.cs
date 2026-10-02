using System;
using System.Reflection;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Espionage;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace DiplomacyIntrigue.Patches
{
    /// <summary>
    /// WHAT: when vanilla picks a hero to raise a new lord's party and the hero it picked runs a
    /// spy network, the pick becomes "nobody" - the clan raises no party that day. Only ever a
    /// veto, and only of a posted handler.
    ///
    /// WHY HARMONY (the lead's call of 2026-10-02, run 11 §8 item 2, reversing story 3.8's D2):
    /// <c>HeroSpawnCampaignBehavior.GetBestAvailableCommander(Clan)</c> searches the clan twice.
    /// The first pass asks <c>Hero.CanLeadParty()</c>, which raises
    /// <c>CampaignEvents.CanHeroLeadPartyEvent</c> - the veto <c>HandlerPostBehavior</c> answers.
    /// The second pass, run only when the first found nobody and the clan is not the player's,
    /// repeats the same filter **without** that call (IL, below). So the event keeps a handler off
    /// a party only while the clan has another free lord; a house whose one free lord is its
    /// handler loses the handler to a party anyway. Run 11 measured that at 3.0 a year across all
    /// realms (`forced-party`), on story 3.8 R5's line. No <c>GameModel</c> decides who commands
    /// (the score is the behaviour's own private method), and no event is raised in the second
    /// pass, so there is nothing else to answer.
    ///
    /// ```
    /// pass 1: IsActive, IsAlive, PartyBelongedTo == null, PartyBelongedToAsPrisoner == null,
    ///         CanLeadParty(), Age > HeroComesOfAge, Occupation == Lord -> best score
    /// if found: return it
    /// if clan == Clan.PlayerClan: return null
    /// pass 2: the same, minus CanLeadParty()                            -> best score
    /// ```
    ///
    /// WHY A POSTFIX THAT RETURNS NULL rather than a second pass of our own that skips handlers:
    /// copying vanilla's filter and score here would be a second copy to drift on the next game
    /// version. The first pass already refuses every handler, so a handler can only come out of
    /// the second; returning null there is the answer the first pass would have given, and the
    /// caller (<c>ConsiderSpawningLordParties</c>, the only call site) treats null as "no commander
    /// today" and simply stops. The clan tries again tomorrow, and a handler recalled from the post
    /// is free to be chosen.
    ///
    /// NOT COVERED, ON PURPOSE: <c>EmptyClanPartiesCampaignBehavior</c> re-spawns a lord's own
    /// cached empty party without asking anyone. A handler with a cached party would arrive there
    /// as `party`, not `forced-party`; run 11 counted 0 of those in ten years.
    ///
    /// VERIFIED AGAINST: the signature <c>private Hero GetBestAvailableCommander(Clan)</c> on the
    /// v1.4.8 reference assemblies (BUTR 1.4.8.119303) and on the v1.5.3 install; the two-pass IL
    /// read on v1.5.3 (tools/CallSites), one call site on both.
    ///
    /// FAILURE MODE: if the method is not found the patch is skipped (<see cref="Prepare"/>) and the
    /// residual is back to being measured, not a crash; on any exception vanilla's pick stands.
    /// </summary>
    [HarmonyPatch]
    public static class HeroSpawn_GetBestAvailableCommander_Patch
    {
        private static MethodBase Target()
            => AccessTools.Method(typeof(HeroSpawnCampaignBehavior), "GetBestAvailableCommander", new[] { typeof(Clan) });

        private static bool Prepare()
        {
            if (Target() != null) return true;
            Log.Warn("Espionage", "HeroSpawnCampaignBehavior.GetBestAvailableCommander(Clan) not found: a handler can again be "
                                  + "forced onto a party when their house has no other free lord.");
            return false;
        }

        private static MethodBase TargetMethod() => Target();

        private static void Postfix(Clan clan, ref Hero __result)
        {
            if (__result == null) return;

            try
            {
                var network = SpyNetworks.PostedNetwork(__result);
                if (network == null) return;

                // Recorded on the same channel the event veto uses, so a refusal from either path
                // reads the same in the log; the daily check then finds the handler still posted.
                SpyNetworks.MarkPartyVetoRefused(__result);
                Log.Debug("Espionage", __result.Name + " runs " + clan?.Name + "'s network in " + network.Target?.Name
                                       + " and is the only lord free to raise a party; the house raises none today.");
                __result = null;
            }
            catch (Exception ex)
            {
                Log.Error("Espionage", "Keeping a handler off a party failed; vanilla's pick stands.", ex);
            }
        }
    }
}
