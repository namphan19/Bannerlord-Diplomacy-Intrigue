using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// The single authority on "is this kingdom allowed to start this war?".
    ///
    /// Two rules, and the split between them is the whole design:
    ///
    ///   - **The routine AI path is blocked.** A kingdom must not wander into a war its
    ///     treaty forbids because the vanilla decision AI happened to roll it. That reads
    ///     as noise, and it makes treaties meaningless.
    ///
    ///   - **Deliberate defiance stays possible.** A vassal that has had enough must be
    ///     able to defy its patron, and a kingdom must be able to break a pact on purpose.
    ///     Those go through <see cref="TreatyRegistry.Break"/> and pay the reputational
    ///     price; they are story beats, not accidents.
    ///
    /// The distinction is enforced by *where* the check is applied: the patches consult
    /// this class, and <see cref="TreatyRegistry.Break"/> does not.
    /// </summary>
    public static class TreatyEnforcement
    {
        /// <summary>
        /// Why a war is blocked, or None when it is allowed. Returned rather than a bare
        /// bool so the patches can log something useful and the UI can explain itself.
        /// </summary>
        public enum Block
        {
            None = 0,
            /// <summary>A live treaty between the two parties forbids it.</summary>
            TreatyForbidsIt = 1,
            /// <summary>The aggressor has handed its foreign policy to a patron.</summary>
            ForeignPolicySubordinated = 2,
            /// <summary>The target is our patron, and defying them is not a routine act.</summary>
            TargetIsOurPatron = 3,
        }

        /// <summary>
        /// True while our own evaluation is deliberately declaring a war.
        ///
        /// War initiation belongs to this mod now: vanilla's proposals are refused, and the
        /// action-level patch refuses anything that reaches it unsanctioned. Our AI calls
        /// the same vanilla action, so it has to be able to say "this one is mine". Scope is
        /// one synchronous call, wrapped in a finally.
        /// </summary>
        public static bool DeclaringSanctionedWar { get; private set; }

        /// <summary>
        /// How many vanilla war proposals have been refused this session, reported weekly.
        ///
        /// Session-scoped rather than saved: it answers a question about the mod's
        /// behaviour, not about the campaign, and putting a diagnostic counter into the save
        /// format would be the wrong trade.
        /// </summary>
        public static int VanillaWarProposalsRefused { get; private set; }

        public static void NoteVanillaProposalRefused() => VanillaWarProposalsRefused++;

        public static void BeginSanctionedWar() => DeclaringSanctionedWar = true;

        public static void EndSanctionedWar() => DeclaringSanctionedWar = false;

        public static Block WhyWarBlocked(ModState state, Kingdom aggressor, Kingdom defender)
        {
            if (state == null || aggressor == null || defender == null || aggressor == defender)
                return Block.None;

            if (state.HasTreatyForbiddingWar(aggressor, defender))
                return Block.TreatyForbidsIt;

            var patron = TreatyRegistry.PatronOf(state, aggressor);
            if (patron != null)
            {
                // A client may not conduct its own foreign policy. Defying the patron
                // directly is a separate, deliberate act - see the class comment.
                if (patron == defender) return Block.TargetIsOurPatron;

                // The patron's own wars reach the client through the call to arms (1.6),
                // not through the client deciding for itself.
                return Block.ForeignPolicySubordinated;
            }

            return Block.None;
        }

        public static bool IsWarAllowed(ModState state, Kingdom aggressor, Kingdom defender)
            => WhyWarBlocked(state, aggressor, defender) == Block.None;

        /// <summary>
        /// Human-readable reason, for logs and for the 1.8 UI. Kept next to the enum so the
        /// two cannot drift apart.
        /// </summary>
        public static string Explain(ModState state, Kingdom aggressor, Kingdom defender, Block block)
        {
            switch (block)
            {
                case Block.TreatyForbidsIt:
                    var treaty = FirstBlockingTreaty(state, aggressor, defender);
                    return treaty == null
                        ? "a standing agreement forbids it"
                        : "the " + treaty.Type + " with " + defender.Name + " forbids it";
                case Block.ForeignPolicySubordinated:
                    var patron = TreatyRegistry.PatronOf(state, aggressor);
                    return aggressor.Name + " answers to " + (patron == null ? "a patron" : patron.Name.ToString())
                           + " and cannot declare war on its own account";
                case Block.TargetIsOurPatron:
                    return defender.Name + " is our patron; defying them is not a routine decision";
                default:
                    return "allowed";
            }
        }

        public static Treaty FirstBlockingTreaty(ModState state, Kingdom a, Kingdom b)
        {
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (treaty.IsActive && treaty.ForbidsWar && treaty.IsBetween(a, b)) return treaty;
            }
            return null;
        }
    }
}
