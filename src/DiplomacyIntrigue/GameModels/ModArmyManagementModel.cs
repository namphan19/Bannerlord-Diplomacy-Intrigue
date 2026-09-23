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
    /// WHAT: keeps the two sides of an internal war out of each other's armies. A rebel lord
    /// cannot raise an army, a rebel party cannot be called into one, and a loyalist lord's
    /// call to arms skips the rebels.
    ///
    /// WHY: `Army` belongs to a kingdom (its only constructor takes one, per ApiDump), and a
    /// rebel is still in the kingdom - so without this, a loyalist marshal would summon the
    /// very lords the crown is at war with, and a rebel lord could raise the parent kingdom's army.
    /// Design 07 §3a's v1 default is that rebels fight as separate parties and form no armies,
    /// until a live test shows vanilla's army code tolerates a rebel-led one.
    ///
    /// WHY A MODEL: these three are virtual on `ArmyManagementCalculationModel`, and CLAUDE.md
    /// §3 puts a game model ahead of Harmony. It also composes: the AI's army creation and the
    /// player's army screen both ask here.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (TaleWorlds.CampaignSystem.ComponentInterfaces
    /// .ArmyManagementCalculationModel; base implementation DefaultArmyManagementCalculationModel).
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
                if (!InternalWars.Any) return allowed;

                if (IsRebel(mobileParty))
                {
                    possibleArmyMembers = new MBList<MobileParty>();
                    return false;
                }

                if (allowed && possibleArmyMembers != null)
                {
                    for (var i = possibleArmyMembers.Count - 1; i >= 0; i--)
                        if (IsRebel(possibleArmyMembers[i])) possibleArmyMembers.RemoveAt(i);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "CanLordCreateArmy failed; falling back to vanilla.", ex);
            }

            return allowed;
        }

        public override bool CheckPartyEligibility(MobileParty party, out TextObject explanation)
        {
            try
            {
                if (InternalWars.Any && IsRebel(party))
                {
                    explanation = new TextObject("Sworn to a rising against the crown.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "CheckPartyEligibility failed; falling back to vanilla.", ex);
            }

            return base.CheckPartyEligibility(party, out explanation);
        }

        public override bool CanPlayerCreateArmy(out TextObject disabledReason)
        {
            try
            {
                if (InternalWars.Any && IsRebel(MobileParty.MainParty))
                {
                    disabledReason = new TextObject("A rising raises no royal army.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "CanPlayerCreateArmy failed; falling back to vanilla.", ex);
            }

            return base.CanPlayerCreateArmy(out disabledReason);
        }

        private static bool IsRebel(MobileParty party)
            => party?.ActualClan != null && InternalWars.TryFaction(party.ActualClan, out _);
    }
}
