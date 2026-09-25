using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Intrigue;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace DiplomacyIntrigue.Statecraft
{
    /// <summary>
    /// Doing trains the skill (design 08 §6). The acts this mod added grant XP to the hero who
    /// performed them, AI heroes included, on vanilla's scale and through vanilla's learning rate:
    /// <c>HeroDeveloper.AddSkillXp</c> with the focus factor applied, which is what
    /// <c>Hero.AddSkillXp</c> itself does. No vanilla hook is relied on - the one that looks
    /// useful, <c>OnInfluenceSpent</c>, passes a negative amount and trains nothing (§1.3).
    ///
    /// Each grant writes <c>[EVENT] kind=skill_xp</c> so a run can total it (§12 point 3).
    /// </summary>
    public static class SkillXp
    {
        public static void Grant(Hero hero, SkillObject skill, float xp, string act)
        {
            if (!StatecraftModel.Enabled || hero == null || skill == null || xp <= 0f) return;
            if (!hero.IsAlive || hero.HeroDeveloper == null) return;

            try
            {
                // Vanilla's own notification, for the player's house only: an AI envoy's Charm
                // rising is not news to the player.
                var notify = hero.Clan != null && hero.Clan == Clan.PlayerClan;
                hero.HeroDeveloper.AddSkillXp(skill, xp, true, notify);

                // A skill that moved can move a Level, and loyalty (S-7) and bloc voice (S-9)
                // read Levels, so the day's bloc memo is stale.
                BlocModel.Invalidate();

                Telemetry.Event("skill_xp", "hero", hero, "clan", hero.Clan, "skill", skill.StringId,
                    "xp", xp, "act", act, "skillNow", hero.GetSkillValue(skill));
            }
            catch (Exception ex)
            {
                Log.Error("Statecraft", "Granting " + skill.StringId + " XP to " + hero.Name + " failed.", ex);
            }
        }

        /// <summary>Grant to whoever holds a realm's portfolio.</summary>
        public static void Grant(Kingdom kingdom, Portfolio portfolio, float xp, string act)
            => Grant(StatecraftModel.Actor(kingdom, portfolio), StatecraftModel.SkillOf(portfolio), xp, act);

        // ----- The acts (design 08 §6) ------------------------------------------------

        /// <summary>A pact signed: the proposer's envoy.</summary>
        public static void PactSigned(Kingdom proposer, TreatyType type)
        {
            float xp;
            switch (type)
            {
                case TreatyType.NonAggressionPact: xp = StatecraftConstants.XpNonAggressionPact; break;
                case TreatyType.DefensivePact: xp = StatecraftConstants.XpDefensivePact; break;
                case TreatyType.Alliance: xp = StatecraftConstants.XpAlliance; break;
                default: return;
            }
            Grant(proposer, Portfolio.Envoy, xp, "pact_" + type);
        }

        /// <summary>A peace signed at the table, not a dormant war's: both envoys, more for a heavier package.</summary>
        public static void PeaceSigned(Kingdom winner, Kingdom loser, float packageCost)
        {
            var xp = StatecraftConstants.XpPeaceBase + StatecraftConstants.XpPeacePerPoint * Math.Max(0f, packageCost);
            Grant(winner, Portfolio.Envoy, xp, "peace_signed");
            Grant(loser, Portfolio.Envoy, xp, "peace_signed");
        }

        public static void TributeDemandAccepted(Kingdom demander)
            => Grant(demander, Portfolio.Envoy, StatecraftConstants.XpTributeDemandAccepted, "tribute_demand");

        public static void SubmissionReceived(Kingdom patron)
            => Grant(patron, Portfolio.Ruler, StatecraftConstants.XpSubmissionReceived, "submission");

        /// <summary>A war ended by peace: each ruler, by how long they carried it.</summary>
        public static void WarEnded(Kingdom a, Kingdom b, float days)
        {
            var xp = Math.Min(StatecraftConstants.XpWarEndedMax, StatecraftConstants.XpWarEndedPerDay * Math.Max(0f, days));
            Grant(a, Portfolio.Ruler, xp, "war_ended");
            Grant(b, Portfolio.Ruler, xp, "war_ended");
        }

        public static void ClaimFabricated(Kingdom fabricator)
            => Grant(fabricator, Portfolio.Spymaster, StatecraftConstants.XpClaimFabricated, "claim_fabricated");

        public static void ClaimExposed(Kingdom fabricator, Kingdom target)
        {
            Grant(fabricator, Portfolio.Spymaster, StatecraftConstants.XpClaimExposed, "claim_exposed");
            Grant(target, Portfolio.Watch, StatecraftConstants.XpFabricationCaught, "fabrication_caught");
        }

        public static void InternalWarWon(Hero leader)
            => Grant(leader, DefaultSkills.Leadership, StatecraftConstants.XpInternalWarWon, "internal_war_won");

        public static void PeaceDividend(Kingdom kingdom)
            => Grant(kingdom, Portfolio.Steward, StatecraftConstants.XpPeaceDividend, "peace_dividend");

        public static void TributePaid(Kingdom payer, Kingdom receiver, int amount)
        {
            var xp = StatecraftConstants.XpPerTributeDenar * amount;
            Grant(payer, Portfolio.Treasurer, xp, "tribute_paid");
            Grant(receiver, Portfolio.Treasurer, xp, "tribute_received");
        }

        public static void HouseBought(Hero buyer, Clan house, int price)
        {
            Grant(StatecraftModel.Actor(buyer?.Clan, Portfolio.Treasurer), DefaultSkills.Trade,
                StatecraftConstants.XpPerSideChangeDenarBuyer * price, "house_bought");
            Grant(house?.Leader, DefaultSkills.Trade,
                StatecraftConstants.XpPerSideChangeDenarHouse * price, "house_sold");
        }
    }
}
