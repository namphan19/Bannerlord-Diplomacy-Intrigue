using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Espionage
{
    /// <summary>
    /// An operation traced back to its owner: the diplomatic incident of design 03 §5, step 3.4.
    ///
    /// Built with the missions rather than after them. The pillar's rule is that espionage is
    /// never a free action, and a mission that could be caught with no consequence would be one:
    /// until this file existed, launching was not worth allowing.
    ///
    /// What an exposure does:
    /// - the victim gains the `EspionageExposed` casus belli against the **owner's realm** - a
    ///   vassal's operation can drag its liege into a war (design 03 §9, decision 5);
    /// - the victim's trust in that realm falls by 25, and after an assassination every other
    ///   realm's falls by 15;
    /// - the handler is taken prisoner where they were stationed - captured, never killed
    ///   (decision 3) - and can be ransomed back in the ordinary way.
    ///
    /// Trust moves through <see cref="TrustRegistry"/>, the one trust ledger. Design 03 §5 calls
    /// the loss "permanent until rebuilt"; the ledger's existing grudge decay applies to it as to
    /// every other grudge, since a second trust rule for spies would be the second-resolver
    /// mistake CLAUDE.md §3 forbids.
    ///
    /// A clan outside any realm has no realm to blame: its handler is still caught and its network
    /// still burned, but there is no one to hold a casus belli against.
    /// </summary>
    public static class Exposure
    {
        public static void Apply(ModState state, SpyMission mission, SpyNetwork network)
        {
            var victim = mission.Target;
            var offender = mission.Owner?.Kingdom;
            var what = Missions.Describe(mission.Type);

            if (offender != null && offender != victim)
            {
                ClaimRegistry.Grant(state, victim, offender, CasusBelliType.EspionageExposed,
                    CampaignTime.YearsFromNow(EspionageConstants.ExposureClaimYears));
                TrustRegistry.Adjust(state, victim, offender, EspionageConstants.ExposureVictimTrust,
                    "caught " + what + " for " + mission.Owner.Name);
                if (mission.Type == SpyMissionType.Assassinate)
                    TrustRegistry.AdjustObservers(state, offender, EspionageConstants.ExposureAssassinationObserverTrust,
                        "an exposed assassination", victim);

                // A vassal house that hands a rival a casus belli against the whole realm answers
                // to its crown for it - the result the lead chose with decision 5. Taken as
                // relation rather than as a grievance the crown holds: nothing reads a grievance
                // held by a crown against its vassal, and relation is what loyalty already reads.
                var ruler = offender.Leader;
                var head = mission.Owner.Leader;
                if (ruler != null && head != null && mission.Owner != offender.RulingClan)
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(ruler, head,
                        EspionageConstants.ExposureVassalRelationPenalty, false);
            }

            var handler = mission.Handler;
            var captured = false;
            if (handler != null && handler.IsAlive && !handler.IsPrisoner)
            {
                var where = handler.CurrentSettlement;
                if (where?.Party != null && where.MapFaction == victim)
                {
                    TakePrisonerAction.Apply(where.Party, handler);
                    captured = handler.IsPrisoner;
                }
            }
            if (network != null && network.Handler == handler)
                SpyNetworks.Release(state, network, captured ? "captured on exposure" : "burned on exposure");

            Log.Info("Espionage", "EXPOSED: " + mission + ". " + victim.Name
                                  + (offender != null && offender != victim
                                      ? " holds EspionageExposed against " + offender.Name + ", trust "
                                        + TrustRegistry.Get(state, victim, offender).ToString("0.0")
                                      : " has no realm to blame")
                                  + "; handler " + (handler == null ? "none" : handler.Name + (captured ? " captured" : " not captured"))
                                  + "; network burned to 0.");

            if (mission.Owner == Clan.PlayerClan)
                Log.Notify("Our agents in " + victim.Name + " were caught " + what + ". The network is burned"
                           + (captured ? ", " + handler.Name + " is their prisoner" : "")
                           + (offender != null ? ", and " + victim.Name + " has a casus belli against " + offender.Name : "")
                           + ".", Colors.Red);
            else if (Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom == victim)
                Log.Notify("We caught agents of " + mission.Owner.Name
                           + (offender != null ? " (" + offender.Name + ")" : "") + " " + what
                           + (captured ? "; " + handler.Name + " is our prisoner" : "")
                           + (offender != null ? ". We hold a casus belli against " + offender.Name : "") + ".", Colors.Green);
            else if (offender != null && Clan.PlayerClan?.Kingdom == offender)
                // A house of the player's own realm was caught: the realm now answers for it. The
                // lead's decision 2 - nothing reaches the player unseen - covers a war handed to
                // them by a vassal as much as an operation aimed at them.
                Log.Notify("Agents of " + mission.Owner.Name + " were caught " + what + " in " + victim.Name + ". "
                           + victim.Name + " now holds a casus belli against " + offender.Name + ".", Colors.Red);
        }
    }
}
