using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// The crown-legitimacy pool: the standing a ruler can lose without losing a battle.
    ///
    /// **One resolver.** Every read and every write goes through here. Phase 1 computed
    /// legitimacy in two places that disagreed, twice, and both times a kingdom honouring a
    /// treaty was punished as an aggressor (CLAUDE.md §3). That was *war* legitimacy, which
    /// `CasusBelli.Resolve` now owns alone; this is *crown* legitimacy, a different number
    /// with the same name, and the way the last one went wrong is the reason this one has a
    /// single door from the first commit.
    ///
    /// The two are related but not interchangeable: `CasusBelli` says how justified a
    /// particular war is, 0-1, and this pool says how legitimate the ruler is, 0-100. War
    /// legitimacy is one of the things that moves the pool.
    /// </summary>
    public static class LegitimacyRegistry
    {
        // ----- Reading --------------------------------------------------------

        /// <summary>
        /// The kingdom's crown legitimacy. A kingdom with no record yet reads as the starting
        /// value rather than zero - a new realm is not an illegitimate one, and a save made
        /// before 2.4 existed must not read as though every crown had collapsed.
        /// </summary>
        public static float Of(ModState state, Kingdom kingdom)
        {
            if (state == null || kingdom == null) return IntrigueConstants.LegitimacyStart;

            for (var i = 0; i < state.Legitimacy.Count; i++)
                if (state.Legitimacy[i].Kingdom == kingdom) return state.Legitimacy[i].Value;

            return IntrigueConstants.LegitimacyStart;
        }

        /// <summary>
        /// Low enough that a pretender can raise a claim in public (design 02 §3, §5). The
        /// claimant half of that condition is 2.5's business; this is only the crown's half.
        /// </summary>
        public static bool IsWeak(ModState state, Kingdom kingdom)
            => Of(state, kingdom) < IntrigueConstants.LegitimacyPretenderThreshold;

        // ----- Writing --------------------------------------------------------

        public static void Adjust(ModState state, Kingdom kingdom, float amount, string reason)
        {
            if (state == null || kingdom == null || amount == 0f) return;

            var record = RecordFor(state, kingdom);
            var before = record.Value;
            record.Adjust(amount, reason);

            // Legitimacy is a term in every loyalty in the kingdom, so the bloc memo is stale.
            BlocModel.Invalidate();

            Log.Info("Legitimacy", kingdom.Name + " " + (amount > 0f ? "+" : "")
                                   + amount.ToString("0.0") + " -> " + record.Value.ToString("0.0")
                                   + " (" + reason + ", was " + before.ToString("0.0") + ")");
        }

        private static KingdomLegitimacy RecordFor(ModState state, Kingdom kingdom)
        {
            for (var i = 0; i < state.Legitimacy.Count; i++)
                if (state.Legitimacy[i].Kingdom == kingdom) return state.Legitimacy[i];

            var created = new KingdomLegitimacy(kingdom, IntrigueConstants.LegitimacyStart);
            state.Legitimacy.Add(created);
            return created;
        }

        // ----- The sources in design 02 §4 ------------------------------------

        /// <summary>
        /// A war ended. The winner gains according to how justified the war was - a win is a
        /// win, but a war nobody could justify buys almost nothing - and the loser pays flat.
        ///
        /// "Won" is read from the war score, the same number the peace table spends. A war
        /// that ends level is nobody's victory and moves neither pool, which is the honest
        /// reading of a white peace and stops two kingdoms both claiming a triumph.
        /// </summary>
        public static void OnWarEnded(ModState state, WarRecord war)
        {
            if (state == null || war == null) return;

            var score = war.ScoreFor(war.Aggressor);
            if (score > -IntrigueConstants.LegitimacyDecisiveScore
                && score < IntrigueConstants.LegitimacyDecisiveScore)
                return;   // a white peace: no victor, no verdict

            var winner = score > 0f ? war.Aggressor : war.Defender;
            var loser = score > 0f ? war.Defender : war.Aggressor;

            var justification = Diplomacy.CasusBelli.Legitimacy(war.Justification);
            var gain = justification >= IntrigueConstants.LegitimacyJustWar
                ? IntrigueConstants.LegitimacyWonJustWar
                : (justification < IntrigueConstants.LegitimacyUnjustWar
                    ? IntrigueConstants.LegitimacyWonUnjustWar
                    : IntrigueConstants.LegitimacyWonOrdinaryWar);

            Adjust(state, winner, gain, "won a war at justification " + justification.ToString("0.00"));
            Adjust(state, loser, -IntrigueConstants.LegitimacyLostWar, "lost a war");
        }

        /// <summary>A war declared with nothing to point at. Design 02 §4.</summary>
        public static void OnWarDeclaredWithoutCause(ModState state, Kingdom aggressor)
            => Adjust(state, aggressor, -IntrigueConstants.LegitimacyNoCasusBelli,
                "declared a war with no casus belli");

        /// <summary>
        /// The heaviest single loss in the table, and deliberately so: a crown that breaks its
        /// word is the one thing this pillar treats as worse than losing.
        /// </summary>
        public static void OnTreatyBroken(ModState state, Kingdom breaker)
            => Adjust(state, breaker, -IntrigueConstants.LegitimacyBrokeTreaty, "broke a treaty");

        /// <summary>
        /// Caught manufacturing a grievance. The Phase 1 hook in <c>ClaimRegistry</c> has been
        /// computing this penalty and only logging it since 1.2; this is where it starts being
        /// paid.
        /// </summary>
        public static void OnCaughtFabricating(ModState state, Kingdom fabricator)
            => Adjust(state, fabricator, -Diplomacy.DiplomacyConstants.FabricateExposedLegitimacyLoss,
                "caught fabricating a claim");

        /// <summary>A fief lost to an outside power. Design 02 §4.</summary>
        public static void OnFiefLost(ModState state, Kingdom kingdom)
            => Adjust(state, kingdom, -IntrigueConstants.LegitimacyLostFief, "lost a fief");

        // ----- Upkeep ---------------------------------------------------------

        /// <summary>
        /// Pays the peace dividend to every kingdom that has gone a full year without a war.
        /// Design 02 §4's "per year of peace".
        ///
        /// Driven daily but paid at most once a year per kingdom, against a stored mark. The
        /// alternatives were both wrong: a 365th paid daily rounds to nothing in a float pool,
        /// and a yearly event pays a kingdom that spent 364 of those days fighting.
        ///
        /// Note what this does *not* do: it cannot run under `diplomacy.tick_days`, because
        /// the mark is a date and `CampaignTime.Now` does not move there. That is stated in
        /// the diagnostic rather than hidden - CLAUDE.md §1's rule about diagnostics that
        /// drive part of a tick.
        /// </summary>
        public static void DailyTick(ModState state)
        {
            if (state == null) return;

            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated) continue;
                if (IsAtWar(state, kingdom)) continue;

                var record = RecordFor(state, kingdom);
                if (record.LastPeaceDividend.ElapsedYearsUntilNow < IntrigueConstants.LegitimacyPeaceDividendYears)
                    continue;

                record.MarkPeaceDividend();
                Adjust(state, kingdom, IntrigueConstants.LegitimacyPeaceDividend, "a year of peace");
            }
        }

        private static bool IsAtWar(ModState state, Kingdom kingdom)
        {
            foreach (var war in state.OngoingWarsOf(kingdom)) return true;
            return false;
        }
    }
}
