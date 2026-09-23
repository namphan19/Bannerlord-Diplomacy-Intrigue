using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// How much power the crown has taken from its lords, −1 to +1.
    ///
    /// **Vanilla has no such number.** Design 02 §3 needs one for the autonomist and
    /// centralist agendas, so it is derived from the policies the kingdom has actually
    /// enacted - real state that the player and the AI both moved, rather than an invented
    /// pool nobody can see or influence. It is therefore already meaningful on a save made
    /// before this mod existed.
    ///
    /// Policies are compared against the <see cref="DefaultPolicies"/> singletons rather than
    /// against string ids. The first draft of this file matched on ids guessed from the
    /// property names; the typed objects are the real API and cannot drift.
    ///
    /// The classification is a **judgement call**, and deliberately a narrow one: only
    /// policies whose direction is unambiguous are counted, so a debatable reading of any one
    /// of them cannot swing the result. A policy not listed is neutral *on this axis*, which
    /// is not a claim that it does nothing.
    /// </summary>
    public static class CrownAuthority
    {
        /// <summary>
        /// −1 (the lords hold everything) to +1 (the crown does). Zero for a kingdom that has
        /// enacted nothing counted here, or whose centralizing and devolving policies cancel.
        ///
        /// Normalised by the number of *counted* policies rather than by a fixed maximum: a
        /// young kingdom with one centralizing policy and nothing else is meaningfully
        /// centralist, and dividing by the whole policy list would call it nearly neutral.
        /// </summary>
        public static float Of(Kingdom kingdom)
        {
            if (kingdom?.ActivePolicies == null) return 0f;

            var crown = 0;
            var lords = 0;

            foreach (var policy in kingdom.ActivePolicies)
            {
                if (policy == null) continue;
                if (IsCrownward(policy)) crown++;
                else if (IsLordward(policy)) lords++;
            }

            var counted = crown + lords;
            return counted == 0 ? 0f : (crown - lords) / (float)counted;
        }

        /// <summary>
        /// Concentrates power in the ruler: a sacred person, land held at the crown's
        /// pleasure, the crown's own commissioners, guard and revenues.
        /// </summary>
        private static bool IsCrownward(PolicyObject policy)
            => policy == DefaultPolicies.SacredMajesty
               || policy == DefaultPolicies.RoyalCommissions
               || policy == DefaultPolicies.RoyalGuard
               || policy == DefaultPolicies.RoyalPrivilege
               || policy == DefaultPolicies.CrownDuty
               || policy == DefaultPolicies.ImperialTowns
               || policy == DefaultPolicies.StateMonopolies
               || policy == DefaultPolicies.PrecarialLandTenure
               || policy == DefaultPolicies.LandTax;

        /// <summary>
        /// Devolves power to the lords and the towns: hereditary tenure, local courts and
        /// charters, councils that bind the ruler, retinues the lords own themselves.
        /// </summary>
        private static bool IsLordward(PolicyObject policy)
            => policy == DefaultPolicies.FeudalInheritance
               || policy == DefaultPolicies.CastleCharters
               || policy == DefaultPolicies.LordsPrivyCouncil
               || policy == DefaultPolicies.CouncilOfTheCommons
               || policy == DefaultPolicies.Lawspeakers
               || policy == DefaultPolicies.Magistrates
               || policy == DefaultPolicies.TrialByJury
               || policy == DefaultPolicies.Cantons
               || policy == DefaultPolicies.NobleRetinues;
    }
}
