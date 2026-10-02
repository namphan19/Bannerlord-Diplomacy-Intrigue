using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
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

            var score = WarScore.For(war, war.Aggressor);
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
        /// A treaty broken, priced by who broke it: a vassal cutting its oath to its patron pays
        /// <see cref="IntrigueConstants.LegitimacyBrokeVassalOath"/>; anyone else breaking anything pays
        /// the full <see cref="IntrigueConstants.LegitimacyBrokeTreaty"/> (the lead, 2026-10-02).
        /// The same price for the player and the AI: it reads the treaty, not the breaker.
        /// </summary>
        public static void OnTreatyBroken(ModState state, Treaty treaty, Kingdom breaker)
        {
            if (treaty != null && treaty.Type == TreatyType.Vassalage && treaty.SubordinateParty == breaker)
                Adjust(state, breaker, -IntrigueConstants.LegitimacyBrokeVassalOath, "a vassal broke its oath");
            else
                OnTreatyBroken(state, breaker);
        }

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
        /// Pays the peace dividend to every kingdom that has gone a full campaign year without
        /// a war. Design 02 §4's "per year of peace".
        ///
        /// Driven daily but paid once per <see cref="IntrigueConstants.LegitimacyPeaceDividendDays"/>
        /// of **continuous** peace, read from <see cref="PeaceOf"/>. The alternatives were both
        /// wrong: an 84th paid daily rounds to nothing in a float pool, and a yearly event pays
        /// a kingdom that spent 83 of those days fighting.
        ///
        /// Until 2026-09-27 the clock only measured the time since the last payment, so a war
        /// neither stopped nor reset it: a realm at war for most of a year was paid on its first
        /// peaceful day once a year had passed (review R-4). The lead chose the spec's reading,
        /// and chose it over the review's proposed mean reversion toward 50, on purpose: a weak
        /// crown must not heal on its own, and civil war is already rare.
        ///
        /// Note what this does *not* do: it cannot run under `diplomacy.tick_days`, because
        /// the clock is dates and `CampaignTime.Now` does not move there. That is stated in
        /// the diagnostic rather than hidden - CLAUDE.md §1's rule about diagnostics that
        /// drive part of a tick.
        /// </summary>
        public static void DailyTick(ModState state)
        {
            if (state == null) return;

            foreach (var kingdom in Kingdom.All)
            {
                if (!kingdom.IsRealm()) continue;
                if (IsAtWar(state, kingdom))
                {
                    WartimeRecovery(state, kingdom);
                    continue;
                }

                // Created here, with its dividend mark at today, the first day a realm is seen at
                // peace - as before. PeaceOf reads a missing record the same way.
                var record = RecordFor(state, kingdom);
                var peace = Clock(state, kingdom, record);
                if (peace.DaysToDividend > 0f) continue;

                record.MarkPeaceDividend();
                Adjust(state, kingdom, PeaceDividendOf(kingdom), "a year of peace");
                Statecraft.SkillXp.PeaceDividend(kingdom);
            }
        }

        /// <summary>
        /// A realm at war heals a little, one step every season and only below a ceiling
        /// (<see cref="IntrigueConstants.LegitimacyWartimeRecovery"/>). Paid on the date, not from a
        /// stored mark, so it needs no save data - and like the dividend it cannot run under
        /// <c>diplomacy.tick_days</c>, whose clock does not move.
        /// </summary>
        private static void WartimeRecovery(ModState state, Kingdom kingdom)
        {
            if (Of(state, kingdom) >= IntrigueConstants.LegitimacyWartimeRecoveryCeiling) return;
            if ((long)CampaignTime.Now.ToDays % IntrigueConstants.LegitimacyWartimeRecoveryDays != 0) return;

            var step = IntrigueConstants.LegitimacyWartimeRecovery
                       * Statecraft.StatecraftTerms.RecoveryFactor(kingdom.RulingClan);
            var room = IntrigueConstants.LegitimacyWartimeRecoveryCeiling - Of(state, kingdom);
            Adjust(state, kingdom, step < room ? step : room, "a season of war, the crown holds on");
        }

        /// <summary>Where a crown stands against its next peace dividend. What <see cref="PeaceOf"/> returns.</summary>
        public sealed class PeaceClock
        {
            /// <summary>At war abroad or with itself: no peace is running, and no dividend comes.</summary>
            public bool AtWar;

            /// <summary>
            /// False when no war of this kingdom's is on record at all, so the start of its
            /// peace is not known - only that it began before this mod started keeping records.
            /// </summary>
            public bool HasWarOnRecord;

            /// <summary>When the present peace began: the end of the kingdom's last war, foreign or internal.</summary>
            public CampaignTime PeaceSince = CampaignTime.Never;

            /// <summary>Days of continuous peace, or 0 at war. Meaningless when <see cref="HasWarOnRecord"/> is false.</summary>
            public float DaysOfPeace;

            /// <summary>
            /// What the dividend clock counts from: the later of the last dividend paid and the
            /// start of this peace. A year of peace already paid for is not paid twice.
            /// </summary>
            public CampaignTime ClockFrom;

            /// <summary>Days until the next dividend, 0 when it is due. Meaningless at war.</summary>
            public float DaysToDividend;
        }

        /// <summary>
        /// How long this crown has been at peace and when its next dividend falls. The one
        /// resolver for it: the daily upkeep pays by it and `diplomacy.legitimacy` prints it.
        ///
        /// **Derived from the war ledgers, not stored.** A peace begins when the kingdom's last
        /// war ended, and every war it fought is still on record with its end date (`WarRecord`
        /// and `InternalWar` are never pruned), so a new saved "peace began" date would be a
        /// second copy of a fact already kept - and would be wrong on every save made before it
        /// existed. The stored dividend mark keeps its meaning (when the dividend was last paid),
        /// which is why no schema change was needed.
        /// </summary>
        public static PeaceClock PeaceOf(ModState state, Kingdom kingdom)
        {
            KingdomLegitimacy record = null;
            if (state != null && kingdom != null)
                for (var i = 0; i < state.Legitimacy.Count; i++)
                    if (state.Legitimacy[i].Kingdom == kingdom) record = state.Legitimacy[i];
            return Clock(state, kingdom, record);
        }

        /// <summary>
        /// <see cref="PeaceOf"/> against a known record. A kingdom with no record yet reads as
        /// the daily upkeep would create it: its dividend clock starting today.
        /// </summary>
        private static PeaceClock Clock(ModState state, Kingdom kingdom, KingdomLegitimacy record)
        {
            var clock = new PeaceClock();
            if (state == null || kingdom == null) return clock;

            if (IsAtWar(state, kingdom))
            {
                clock.AtWar = true;
                return clock;
            }

            for (var i = 0; i < state.Wars.Count; i++)
            {
                var war = state.Wars[i];
                if (war.IsOngoing || !war.Involves(kingdom)) continue;
                Later(clock, war.EndedOn);
            }
            for (var i = 0; i < state.InternalWars.Count; i++)
            {
                var war = state.InternalWars[i];
                if (war.IsOngoing || war.Kingdom != kingdom) continue;
                Later(clock, war.EndedOn);
            }

            var now = CampaignTime.Now;
            if (clock.HasWarOnRecord) clock.DaysOfPeace = (float)(now - clock.PeaceSince).ToDays;

            var mark = record?.LastPeaceDividend ?? now;
            clock.ClockFrom = clock.HasWarOnRecord && clock.PeaceSince > mark ? clock.PeaceSince : mark;

            var left = IntrigueConstants.LegitimacyPeaceDividendDays - (float)(now - clock.ClockFrom).ToDays;
            clock.DaysToDividend = left > 0f ? left : 0f;
            return clock;
        }

        private static void Later(PeaceClock clock, CampaignTime ended)
        {
            if (ended == CampaignTime.Never) return;
            if (clock.HasWarOnRecord && ended <= clock.PeaceSince) return;
            clock.PeaceSince = ended;
            clock.HasWarOnRecord = true;
        }

        /// <summary>
        /// What a year of peace restores to this crown: the base dividend at the pace of the ruling
        /// house's steward (design 08 S-6). The upkeep and the Court tab's note both read this.
        /// </summary>
        public static float PeaceDividendOf(Kingdom kingdom)
            => IntrigueConstants.LegitimacyPeaceDividend
               * Statecraft.StatecraftTerms.RecoveryFactor(kingdom?.RulingClan);

        /// <summary>
        /// At war abroad, or with itself. An internal war has no `WarRecord` (design 07 §3a Q4),
        /// so reading the war ledger alone would pay a realm in civil war its peace dividend.
        /// </summary>
        private static bool IsAtWar(ModState state, Kingdom kingdom)
        {
            foreach (var war in state.OngoingWarsOf(kingdom)) return true;
            return InternalWars.OngoingIn(state, kingdom) != null;
        }
    }
}
