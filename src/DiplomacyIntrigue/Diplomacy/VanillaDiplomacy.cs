using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// Shared state for the game-model overrides that take inter-kingdom diplomacy away from
    /// vanilla. See docs/design/05-vanilla-override.md for the full inventory.
    ///
    /// **Why this exists at all.** Balance run 02 measured 145 of 167 wars (86.8%) ending
    /// without our peace table being involved, at a median length of six days, with 46 of them
    /// dying on the day they started at war score 0.00. We had taken war *declaration* and left
    /// vanilla holding peace, alliances, trade agreements and its own call-to-war. Two systems
    /// with opinions about the same relationship produce the worse of the two.
    ///
    /// The counters here are session-scoped rather than saved: they answer a question about the
    /// mod's behaviour, not about the campaign, and a diagnostic has no business in a save
    /// format. They are the acceptance measure for the takeover - if they stay at zero in a
    /// long run, either vanilla never wanted these things or our overrides are not installed,
    /// and those two look identical from the outside unless we count.
    /// </summary>
    public static class VanillaDiplomacy
    {
        public static int PeaceRefused { get; private set; }
        public static int AllianceRefused { get; private set; }
        public static int TradeAgreementRefused { get; private set; }
        public static int CallToWarRefused { get; private set; }

        public static void NotePeaceRefused() => PeaceRefused++;
        public static void NoteAllianceRefused() => AllianceRefused++;
        public static void NoteTradeAgreementRefused() => TradeAgreementRefused++;
        public static void NoteCallToWarRefused() => CallToWarRefused++;

        /// <summary>
        /// Whether the overrides should act at all. When the mod failed to start, or the
        /// player switched the pillar off, vanilla keeps its own diplomacy - a half-running
        /// mod that has blocked peace would be far worse than one that is simply off.
        /// </summary>
        public static bool Active =>
            SubModule.Healthy && Settings.Current.EnableDiplomacy && CoreBehavior.State != null;

        /// <summary>
        /// True only for kingdom-versus-kingdom. Clans, minor factions and rebels keep
        /// vanilla's diplomacy: the mod models kingdoms, and blocking peace for factions it
        /// does not model would strand them at war with no way out.
        /// </summary>
        public static bool BothKingdoms(IFaction a, IFaction b)
            => a is Kingdom && b is Kingdom && a != b;
    }
}
