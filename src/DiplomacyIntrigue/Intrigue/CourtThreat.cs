using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// When an AI ruler's court is in danger, which houses are, and what the ruler keeps back
    /// before spending on them (design 09 D6). One answer, read by both of the AI's court acts -
    /// making amends (C1) and giving a seat (C2) - so the two cannot disagree about whether the
    /// court is under threat or how much the crown may spend.
    /// </summary>
    public static class CourtThreat
    {
        /// <summary>
        /// Houses in danger: every member of a Pretenders bloc, and every sworn house below the
        /// defection line. Empty when the court is not under threat.
        /// </summary>
        public static HashSet<Clan> DangerHouses(ModState state, Kingdom kingdom)
        {
            var danger = new HashSet<Clan>();
            var ruling = kingdom?.RulingClan;
            if (state == null || ruling == null) return danger;

            foreach (var bloc in BlocModel.BlocsOf(state, kingdom))
                if (bloc.Agenda == CourtAgenda.Pretenders)
                    foreach (var member in bloc.Members) danger.Add(member);

            var clans = kingdom.Clans;
            for (var i = 0; i < clans.Count; i++)
            {
                var clan = clans[i];
                if (!Court.IsMember(clan) || clan == ruling) continue;
                if (LoyaltyModel.Of(state, clan) < IntrigueConstants.LoyaltyDisaffected) danger.Add(clan);
            }
            return danger;
        }

        /// <summary>
        /// Influence an AI ruler keeps: twice its war-declaration cost on a Conquest claim, so a
        /// court act never leaves it unable to go to war.
        /// </summary>
        public static float InfluenceReserve(ModState state, Kingdom kingdom)
            => IntrigueConstants.AiAmendsWarCostReserve
               * AiDiplomacy.WarDeclarationCost(state, kingdom, CasusBelli.Legitimacy(CasusBelliType.Conquest));

        /// <summary>True when the crown can pay both parts and still hold both reserves.</summary>
        public static bool CanSpend(ModState state, Kingdom kingdom, int influence, int gold)
        {
            var ruling = kingdom?.RulingClan;
            var ruler = kingdom?.Leader;
            if (ruling == null || ruler == null) return false;
            return ruling.Influence - influence >= InfluenceReserve(state, kingdom)
                   && ruler.Gold - gold >= DiplomacyConstants.AiGoldReserve;
        }

        /// <summary>The reserves in words, for the diagnostics.</summary>
        public static string Reserves(ModState state, Kingdom kingdom)
            => InfluenceReserve(state, kingdom).ToString("0") + " influence, "
               + DiplomacyConstants.AiGoldReserve.ToString("N0") + " denars";
    }
}
