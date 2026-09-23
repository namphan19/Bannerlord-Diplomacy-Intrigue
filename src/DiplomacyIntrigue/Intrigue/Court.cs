using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// Who sits at a kingdom's court. The one definition every part of this pillar uses.
    ///
    /// A clan serving a kingdom **as a mercenary** is in <c>Kingdom.Clans</c> and is not at
    /// court: it holds no fief from the crown, cannot vote or propose in a kingdom decision in
    /// vanilla, and leaves when the contract ends. Found by reading a live diagnostic rather
    /// than by design - the Legion of the Betrayed and Skolderbroda were being given loyalty
    /// scores, sorted into blocs and counted at successions in the Northern Empire's court.
    ///
    /// Written as one helper rather than a mercenary check added to each loop, because "is
    /// this clan part of the court" is a single question with nine callers, and nine private
    /// copies of it is exactly the shape CLAUDE.md §3 warns about.
    /// </summary>
    public static class Court
    {
        public static bool IsMember(Clan clan)
            => clan != null
               && !clan.IsEliminated
               && clan.Kingdom != null
               && !clan.IsUnderMercenaryService;

        /// <summary>The clans of <paramref name="kingdom"/> that sit at its court.</summary>
        public static IEnumerable<Clan> MembersOf(Kingdom kingdom)
        {
            if (kingdom?.Clans == null) yield break;

            for (var i = 0; i < kingdom.Clans.Count; i++)
            {
                var clan = kingdom.Clans[i];
                if (IsMember(clan)) yield return clan;
            }
        }
    }
}
