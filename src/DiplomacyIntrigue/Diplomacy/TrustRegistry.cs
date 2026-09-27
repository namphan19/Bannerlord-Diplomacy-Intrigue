using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// The reputation ledger. Trust is the mod's answer to a question vanilla never asks:
    /// what does it cost, long-term, to be the kingdom that breaks its word?
    ///
    /// Relation already covers short-term feeling and fades. Trust is reputation: it
    /// outlives the season, but since run 06 it is no longer a ratchet - it drifts toward
    /// zero for any pair nobody is tending, and a war bleeds it faster the longer the war
    /// runs (<see cref="DailyTick"/>). A betrayal is still remembered for years, just not
    /// literally forever - and the punishment for treachery is still not a modifier, it is
    /// that nobody will sign anything with you (see <see cref="WillConsiderPacts"/>).
    /// </summary>
    public static class TrustRegistry
    {
        // ----- Reading --------------------------------------------------------

        /// <summary>What <paramref name="from"/> thinks of <paramref name="to"/>. Zero by default.</summary>
        public static float Get(ModState state, Kingdom from, Kingdom to)
        {
            if (from == null || to == null || from == to) return 0f;

            for (var i = 0; i < state.Trust.Count; i++)
                if (state.Trust[i].Is(from, to)) return state.Trust[i].Value;
            return 0f;
        }

        /// <summary>
        /// False when <paramref name="from"/> distrusts <paramref name="to"/> too much to
        /// sign a <paramref name="type"/> with it. A truce is always available: ending a war
        /// has to stay possible however badly the parties have behaved, or wars become
        /// unendable - <see cref="TreatyRegistry.CanSign"/> never asks this for one.
        /// </summary>
        public static bool WillConsiderPacts(ModState state, Kingdom from, Kingdom to, TreatyType type)
            => Get(state, from, to) > PactFloor(state, from, to, type, out _, out _);

        /// <summary>
        /// The trust below which <paramref name="from"/> will not sign <paramref name="type"/> with
        /// <paramref name="to"/>. <see cref="DiplomacyConstants.TrustFloorForPacts"/> for every
        /// type but one.
        ///
        /// R-9, 2026-09-27: a **defensive pact** against a real balancing threat has a lower
        /// floor, by <see cref="DiplomacyConstants.DefensivePactTrustFloorRelief"/> times the
        /// pull. The pull and the sphere it names are <see cref="AiDiplomacy.BalancingPull"/>'s,
        /// symmetric in the pair, so both directions of a signature read the same relief. After a
        /// great war most of the realms that must stand against the winner have just fought each
        /// other, and the plain floor vetoed the very coalition the pull asks for (design 04
        /// §12.5). Only the defensive pact: it obliges each side to guard the other, which the
        /// threat justifies; an alliance asks them to follow each other into wars of choice, and a
        /// non-aggression pact does nothing against the threat at all.
        ///
        /// No "is this the player" anywhere: the player's pact chooser and the AI's weekly scan
        /// both reach this through <see cref="TreatyRegistry.CanSign"/>.
        /// </summary>
        public static float PactFloor(ModState state, Kingdom from, Kingdom to, TreatyType type,
            out float pull, out Kingdom against)
        {
            pull = 0f;
            against = null;
            if (type != TreatyType.DefensivePact) return DiplomacyConstants.TrustFloorForPacts;

            pull = AiDiplomacy.BalancingPull(state, from, to, out against);
            if (against == null || pull <= 0f)
            {
                pull = 0f;
                return DiplomacyConstants.TrustFloorForPacts;
            }
            return DiplomacyConstants.TrustFloorForPacts - pull * DiplomacyConstants.DefensivePactTrustFloorRelief;
        }

        // ----- Writing --------------------------------------------------------

        /// <summary>Adjusts one direction only.</summary>
        public static void Adjust(ModState state, Kingdom from, Kingdom to, float amount, string reason)
        {
            if (from == null || to == null || from == to || amount == 0f) return;

            var record = RecordFor(state, from, to);
            record.Add(amount);
            LogChange(from, to, amount, record.Value, reason);
        }

        /// <summary>Adjusts both directions, for events that reflect equally on each party.</summary>
        public static void AdjustMutual(ModState state, Kingdom a, Kingdom b, float amount, string reason)
        {
            Adjust(state, a, b, amount, reason);
            Adjust(state, b, a, amount, reason);
        }

        /// <summary>
        /// Applies a change in the eyes of every kingdom that is not a party to the event.
        /// This is how reputation becomes structural rather than personal: breaking a
        /// treaty does not just anger the victim, it tells eleven other courts what you are.
        /// </summary>
        public static void AdjustObservers(ModState state, Kingdom subject, float amount, string reason,
            Kingdom alsoExclude = null)
        {
            if (subject == null || amount == 0f) return;

            foreach (var observer in Kingdom.All)
            {
                if (observer == subject || observer == alsoExclude) continue;
                if (!observer.IsRealm()) continue;
                Adjust(state, observer, subject, amount, reason);
            }
        }

        // ----- Event handlers, one per line of the design table ---------------

        public static void OnTreatyHonoured(ModState state, Treaty treaty)
        {
            AdjustMutual(state, treaty.PartyA, treaty.PartyB,
                DiplomacyConstants.TrustTreatyHonoured, "honoured " + treaty.Type + " to expiry");
        }

        public static void OnTreatyBroken(ModState state, Treaty treaty, Kingdom breaker)
        {
            var victim = treaty.Other(breaker);
            if (victim == null) return;

            Adjust(state, victim, breaker,
                DiplomacyConstants.TrustTreatyBrokenVictim, "broke our " + treaty.Type);
            AdjustObservers(state, breaker,
                DiplomacyConstants.TrustTreatyBrokenObserver, "broke a " + treaty.Type, victim);
        }

        /// <summary>
        /// A war nobody can justify offends every court that is not in it. This is the
        /// mechanism that isolates serial aggressors, instead of a hidden hand-of-god
        /// penalty on the aggressor's numbers.
        /// </summary>
        public static void OnWarDeclared(ModState state, Kingdom aggressor, Kingdom defender, float legitimacy)
        {
            if (legitimacy >= DiplomacyConstants.UnjustWarLegitimacyThreshold) return;

            AdjustObservers(state, aggressor,
                DiplomacyConstants.TrustUnjustWarObserver, "declared an unjustified war", defender);
        }

        public static void OnPeaceHeld(ModState state, Kingdom a, Kingdom b)
        {
            AdjustMutual(state, a, b, DiplomacyConstants.TrustPeaceHeld,
                DiplomacyConstants.PeaceDividendYears + " years of peace");
        }

        public static void OnCallToArmsAnswered(ModState state, Kingdom caller, Kingdom answerer)
        {
            Adjust(state, caller, answerer, DiplomacyConstants.TrustCallToArmsAnswered, "answered our call to arms");
        }

        public static void OnCallToArmsRefused(ModState state, Kingdom caller, Kingdom refuser)
        {
            // Deliberately not treated as a breach. Refusal has to be a real option, or an
            // alliance is a suicide pact - so it costs trust and nothing else.
            Adjust(state, caller, refuser, DiplomacyConstants.TrustCallToArmsRefused, "refused our call to arms");
        }

        // ----- Daily decay ----------------------------------------------------

        /// <summary>
        /// Fades every record a little, once a day - the lead's F2 decision after run 06,
        /// where trust only ever grew and saturated near +100 across the map.
        ///
        /// Three rules:
        ///
        ///   - At peace a record drifts toward zero, from either side. A reputation not
        ///     being maintained fades - and so does a grudge, which is the ledger's only
        ///     way back from the bottom. A grudge fades at a quarter of the speed
        ///     (<see cref="DiplomacyConstants.TrustGrudgeDecayPerDay"/>).
        ///   - A positive change suspends that peacetime drift for
        ///     <see cref="DiplomacyConstants.TrustDecayGraceDays"/> days, so a relationship
        ///     still being tended never drains.
        ///   - At war the record moves *down* instead - goodwill erodes, enmity deepens -
        ///     and the bleed grows with each day the war has run, down to
        ///     <see cref="DiplomacyConstants.TrustWarFloor"/> and no further. Deeper than that
        ///     is what treachery earns, not fighting. **The grace does not shield a war**
        ///     (2026-09-27, TODO 7): the war is checked first and bleeds from its first day.
        ///
        /// Why the war comes before the grace. With the grace checked first, a pact seen
        /// through to expiry paid its +12 "honoured" and opened 30 days in which nothing could
        /// move the record - so a kingdom that attacked its former partner days after the pact
        /// lapsed fought the first month of that war for free. The 2026-09-19 max-speed run saw
        /// it three times, each attack ~6 days after a defensive pact expired. The +12 itself is
        /// kept, not clawed back: the pact *was* honoured to its end, and what went wrong was
        /// only that the war that followed cost nothing. A grace is a reward for tending a
        /// relationship; a war is not tending it.
        ///
        /// A pair at war with no record yet bleeds too. <see cref="Get"/> already reads a
        /// missing record as zero, so skipping it here would make "no history" the one
        /// state a war could not touch - and a record that peace had drifted to exactly zero
        /// was skipped the same way before this.
        ///
        /// Silent on purpose: one log line per record per day would drown the log, and the
        /// weekly snapshot's trustIn/trustOut is where the drift is meant to be read.
        /// </summary>
        public static void DailyTick(ModState state)
        {
            for (var i = 0; i < state.Wars.Count; i++)
            {
                var ongoing = state.Wars[i];
                if (!ongoing.IsOngoing) continue;
                RecordFor(state, ongoing.Aggressor, ongoing.Defender);
                RecordFor(state, ongoing.Defender, ongoing.Aggressor);
            }

            for (var i = 0; i < state.Trust.Count; i++)
            {
                var record = state.Trust[i];
                var from = record.From;
                var to = record.To;
                if (from == null || to == null) continue;

                var war = state.OngoingWarBetween(from, to);
                if (war == null && record.Value == 0f) continue;

                // Before the grace, not after it: see the summary above.
                if (war != null)
                {
                    // Down to the war floor and no further; a breach may already sit below it.
                    var headroom = record.Value - DiplomacyConstants.TrustWarFloor;
                    if (headroom > 0f)
                        record.Decay(-System.Math.Min(headroom,
                            DiplomacyConstants.TrustDecayWarPerDay
                            + war.DaysElapsed * DiplomacyConstants.TrustDecayWarRampPerDay));
                    continue;
                }

                var daysSincePositive =
                    (float)(CampaignTime.Now - record.LastPositiveChange).ToDays;
                if (daysSincePositive < DiplomacyConstants.TrustDecayGraceDays) continue;

                var value = record.Value;
                record.Decay(value > 0f
                    ? -System.Math.Min(DiplomacyConstants.TrustGoodwillDecayPerDay, value)
                    : System.Math.Min(DiplomacyConstants.TrustGrudgeDecayPerDay, -value));
            }
        }

        // ----- Refused offers -------------------------------------------------

        /// <summary>
        /// The player turned down <paramref name="candidate"/>'s offer to kneel: the trust
        /// cost, and the start of the cooldown before it asks again.
        /// </summary>
        public static void OnOfferRefused(ModState state, Kingdom candidate, Kingdom playerRealm, string reason)
        {
            Adjust(state, candidate, playerRealm, DiplomacyConstants.TrustOfferRefused, reason);
            RecordFor(state, candidate, playerRealm)?.MarkOfferRefused();
        }

        /// <summary>
        /// Whether <paramref name="candidate"/> was turned down by <paramref name="playerRealm"/>
        /// recently enough that it will not offer again yet
        /// (<see cref="DiplomacyConstants.PlayerOfferRefusalCooldownDays"/>).
        /// </summary>
        public static bool RefusedOfferRecently(ModState state, Kingdom candidate, Kingdom playerRealm)
        {
            if (candidate == null || playerRealm == null) return false;
            for (var i = 0; i < state.Trust.Count; i++)
            {
                if (!state.Trust[i].Is(candidate, playerRealm)) continue;
                var days = (float)(CampaignTime.Now - state.Trust[i].LastOfferRefused).ToDays;
                return days < DiplomacyConstants.PlayerOfferRefusalCooldownDays;
            }
            return false;
        }

        /// <summary>The record for this ordered pair, created at zero if there is none yet.</summary>
        private static TrustRecord RecordFor(ModState state, Kingdom from, Kingdom to)
        {
            if (from == null || to == null || from == to) return null;
            for (var i = 0; i < state.Trust.Count; i++)
                if (state.Trust[i].Is(from, to)) return state.Trust[i];

            var record = new TrustRecord(from, to, 0f);
            state.Trust.Add(record);
            return record;
        }

        private static void LogChange(Kingdom from, Kingdom to, float amount, float now, string reason)
        {
            if (!Settings.Current.VerboseLogging) return;
            Log.Debug("Trust", from.Name + " -> " + to.Name + ": "
                               + (amount > 0 ? "+" : "") + amount.ToString("0.0")
                               + " (" + reason + ") now " + now.ToString("0.0"));
        }
    }
}
