using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// The reputation ledger. Trust is the mod's answer to a question vanilla never asks:
    /// what does it cost, long-term, to be the kingdom that breaks its word?
    ///
    /// Relation already covers short-term feeling and fades. Trust does not fade, so a
    /// betrayal in year three is still remembered in year twenty - and the punishment is
    /// not a modifier, it is that nobody will sign anything with you (see
    /// <see cref="WillConsiderPacts"/>).
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
        /// sign anything but a truce. A truce is always available: ending a war has to stay
        /// possible however badly the parties have behaved, or wars become unendable.
        /// </summary>
        public static bool WillConsiderPacts(ModState state, Kingdom from, Kingdom to)
            => Get(state, from, to) > DiplomacyConstants.TrustFloorForPacts;

        // ----- Writing --------------------------------------------------------

        /// <summary>Adjusts one direction only.</summary>
        public static void Adjust(ModState state, Kingdom from, Kingdom to, float amount, string reason)
        {
            if (from == null || to == null || from == to || amount == 0f) return;

            for (var i = 0; i < state.Trust.Count; i++)
            {
                if (!state.Trust[i].Is(from, to)) continue;
                state.Trust[i].Add(amount);
                LogChange(from, to, amount, state.Trust[i].Value, reason);
                return;
            }

            var record = new TrustRecord(from, to, 0f);
            record.Add(amount);
            state.Trust.Add(record);
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
                if (observer.IsEliminated) continue;
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

        private static void LogChange(Kingdom from, Kingdom to, float amount, float now, string reason)
        {
            if (!Settings.Current.VerboseLogging) return;
            Log.Debug("Trust", from.Name + " -> " + to.Name + ": "
                               + (amount > 0 ? "+" : "") + amount.ToString("0.0")
                               + " (" + reason + ") now " + now.ToString("0.0"));
        }
    }
}
