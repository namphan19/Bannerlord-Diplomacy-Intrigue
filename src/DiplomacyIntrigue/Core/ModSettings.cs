using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace DiplomacyIntrigue.Core
{
    /// <summary>
    /// Player-facing configuration, surfaced through Mod Configuration Menu.
    ///
    /// Two rules for this class:
    ///  - Never read it directly from gameplay code. Go through <see cref="Settings"/>,
    ///    which falls back to defaults when MCM is unavailable.
    ///  - Changing Id resets everyone saved settings, so leave it alone.
    /// </summary>
    public sealed class ModSettings : AttributeGlobalSettings<ModSettings>
    {
        public override string Id => "DiplomacyIntrigue_v1";
        public override string DisplayName => "Diplomacy & Intrigue";
        public override string FolderName => "DiplomacyIntrigue";
        public override string FormatType => "json2";

        private const string SystemsGroup = "Systems";
        private const string PaceGroup = "Pacing";
        private const string DebugGroup = "Diagnostics";

        [SettingPropertyBool("Diplomacy", Order = 0, RequireRestart = true,
            HintText = "Treaties, casus belli, war exhaustion and the peace table.")]
        [SettingPropertyGroup(SystemsGroup)]
        public bool EnableDiplomacy { get; set; } = true;

        [SettingPropertyBool("Court intrigue", Order = 1, RequireRestart = true,
            HintText = "Vassal grievances, court blocs, succession disputes and civil war.")]
        [SettingPropertyGroup(SystemsGroup)]
        public bool EnableIntrigue { get; set; } = true;

        [SettingPropertyBool("Espionage", Order = 2, RequireRestart = true,
            HintText = "Spy networks, covert missions and counter-intelligence.")]
        [SettingPropertyGroup(SystemsGroup)]
        public bool EnableEspionage { get; set; } = true;

        /// <summary>
        /// Design 08 (Phase 2.8). Not a restart setting: off makes every skill term neutral and
        /// stops the XP grants, and nothing is saved by it, so it can be flipped mid-campaign. That
        /// is also how S3's control run is made.
        /// </summary>
        [SettingPropertyBool("Statecraft", Order = 3, RequireRestart = false,
            HintText = "Your heroes' skills count in politics: the ruler's Leadership, and the best "
                       + "Charm, Steward, Trade, Roguery and Scouting in the ruling house. Off, skills "
                       + "change nothing here and political acts train nothing.")]
        [SettingPropertyGroup(SystemsGroup)]
        public bool EnableStatecraft { get; set; } = true;

        [SettingPropertyFloatingInteger("War exhaustion rate", 0.25f, 4f, "0.00", Order = 0,
            RequireRestart = false,
            HintText = "Multiplier on how fast wars wear kingdoms down. Higher means shorter wars.")]
        [SettingPropertyGroup(PaceGroup)]
        public float WarExhaustionRate { get; set; } = 1f;

        [SettingPropertyFloatingInteger("AI diplomacy aggressiveness", 0.25f, 4f, "0.00", Order = 1,
            RequireRestart = false,
            HintText = "Multiplier on how readily AI kingdoms seek wars over pacts.")]
        [SettingPropertyGroup(PaceGroup)]
        public float AiAggressiveness { get; set; } = 1f;

        [SettingPropertyBool("Verbose logging", Order = 0, RequireRestart = false,
            HintText = "Writes every diplomacy decision to the Diplomacy & Intrigue log. Slower, but needed for bug reports.")]
        [SettingPropertyGroup(DebugGroup)]
        public bool VerboseLogging { get; set; } = false;

        [SettingPropertyBool("Show AI decisions in message log", Order = 1, RequireRestart = false,
            HintText = "Prints AI treaty and war decisions to the in-game message log.")]
        [SettingPropertyGroup(DebugGroup)]
        public bool AnnounceAiDecisions { get; set; } = false;

        [SettingPropertyBool("Write telemetry to the log", Order = 2, RequireRestart = false,
            HintText = "One line per week plus one per war that ends, so a long campaign can be "
                       + "measured afterwards. Costs almost nothing and is what balance reports are built from.")]
        [SettingPropertyGroup(DebugGroup)]
        public bool EnableTelemetry { get; set; } = true;
    }

    /// <summary>
    /// Single access point for configuration. MCM can be missing, disabled, or throw during
    /// early startup, so gameplay code never touches <see cref="ModSettings"/> directly.
    /// </summary>
    public static class Settings
    {
        private static readonly ModSettings Fallback = new ModSettings();

        public static ModSettings Current
        {
            get
            {
                try
                {
                    return ModSettings.Instance ?? Fallback;
                }
                catch
                {
                    return Fallback;
                }
            }
        }
    }
}
