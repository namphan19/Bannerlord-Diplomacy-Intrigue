using DiplomacyIntrigue.Intrigue;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Core
{
    /// <summary>
    /// Which kingdoms this mod treats as realms. One definition, read by every loop over
    /// `Kingdom.All` in every pillar.
    ///
    /// A kingdom is a realm unless it is eliminated or is the map faction of an internal war
    /// (<see cref="InternalWars.IsFaction"/>). A rising is a real `Kingdom` object - it has to
    /// be, because vanilla casts map factions to `Kingdom` (design 07 §3c) - but it is not a
    /// realm: it holds no court, signs no treaty, owes no tribute, makes no claim and is never
    /// a target of this mod's diplomacy. Its clans are still members of the realm they rose in.
    ///
    /// Written as one helper rather than a second condition added to forty loops, for the same
    /// reason as <see cref="Court.IsMember"/>: "is this a realm" is a single question, and forty
    /// private copies of it is the shape CLAUDE.md §3 warns about.
    /// </summary>
    public static class Realms
    {
        public static bool IsRealm(this Kingdom kingdom)
            => kingdom != null && !kingdom.IsEliminated && !InternalWars.IsFaction(kingdom);
    }
}
