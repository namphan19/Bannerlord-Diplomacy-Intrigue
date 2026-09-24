using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Intrigue;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace DiplomacyIntrigue.GameModels
{
    /// <summary>
    /// WHAT: keeps the two sides of an internal war out of each other's armies. Each side may
    /// raise armies of its own; neither may call the other's parties into them.
    ///
    /// WHY: a rebel is still a member of the realm, so the realm's list of war parties still
    /// holds every rebel party. A loyalist marshal's call reads that list
    /// (`CanLordCreateArmy` takes its candidates from `MapFaction.WarPartyComponents`), and would
    /// summon the very lords the crown is at war with. A rebel marshal's call reads the rising's
    /// list, which holds only rebels, so that side needs no filter - it is filtered anyway,
    /// because one rule for both sides is easier to trust than two.
    ///
    /// **The first version forbade rebel armies outright**, on the reasoning that an `Army`
    /// belongs to a kingdom and a rebel's kingdom is the realm it fights. That stopped being true
    /// when the rising became a real kingdom (design 07 §3d): the AI raises an army with
    /// `((Kingdom)party.MapFaction).CreateArmy(...)`, which for a rebel is the rising. The ban
    /// then cost the war its sieges - two live runs, 303 battles and 59 raids between the sides,
    /// and not one siege, since the AI besieges only with armies.
    ///
    /// WHY A MODEL: these are virtual on `ArmyManagementCalculationModel`, and CLAUDE.md §3 puts
    /// a game model ahead of Harmony.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (TaleWorlds.CampaignSystem.ComponentInterfaces
    /// .ArmyManagementCalculationModel; base implementation DefaultArmyManagementCalculationModel;
    /// `CheckPartyEligibility`'s only caller is the player's `ArmyManagementItemVM`).
    ///
    /// FAILURE MODE: every override falls through to base on exception.
    /// </summary>
    public sealed class ModArmyManagementModel : DefaultArmyManagementCalculationModel
    {
        public override bool CanLordCreateArmy(MobileParty mobileParty, out MBList<MobileParty> possibleArmyMembers)
        {
            var allowed = base.CanLordCreateArmy(mobileParty, out possibleArmyMembers);

            try
            {
                if (!InternalWars.Any || !allowed || possibleArmyMembers == null) return allowed;

                var side = SideOf(mobileParty);
                for (var i = possibleArmyMembers.Count - 1; i >= 0; i--)
                    if (SideOf(possibleArmyMembers[i]) != side) possibleArmyMembers.RemoveAt(i);
            }
            catch (Exception ex)
            {
                Log.Error("Override", "CanLordCreateArmy failed; falling back to vanilla.", ex);
            }

            return allowed;
        }

        /// <summary>
        /// The player's army screen. Only it calls this, so "the other side" is measured against
        /// the player's own party.
        /// </summary>
        public override bool CheckPartyEligibility(MobileParty party, out TextObject explanation)
        {
            try
            {
                if (InternalWars.Any && party != null && MobileParty.MainParty != null
                    && SideOf(party) != SideOf(MobileParty.MainParty))
                {
                    explanation = new TextObject("On the other side of the civil war.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "CheckPartyEligibility failed; falling back to vanilla.", ex);
            }

            return base.CheckPartyEligibility(party, out explanation);
        }

        /// <summary>
        /// Which side of an internal war a party fights on: the rising a rebel answers to, or the
        /// realm of anyone else. Read from the same index as the map-faction patches.
        /// </summary>
        private static IFaction SideOf(MobileParty party)
        {
            var clan = party?.ActualClan;
            if (clan == null) return null;
            if (InternalWars.TryFaction(clan, out var rising)) return rising;
            return clan.Kingdom;
        }
    }
}
