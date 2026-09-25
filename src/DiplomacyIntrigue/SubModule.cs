using System;
using System.Reflection;
using HarmonyLib;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.GameModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using DiplomacyIntrigue.UI;

namespace DiplomacyIntrigue
{
    /// <summary>
    /// Module entry point. Responsibilities, in order of the engine's lifecycle:
    ///   OnSubModuleLoad                      - logging, Harmony patching
    ///   OnBeforeInitialModuleScreenSetAsRoot - one-shot main-menu notice
    ///   OnGameStart                          - register campaign behaviors and model overrides
    /// Everything here is wrapped: a throw in a SubModule hook takes the whole game down.
    /// </summary>
    public sealed class SubModule : MBSubModuleBase
    {
        public const string ModuleId = "DiplomacyIntrigue";
        public const string ModuleVersion = "0.1.0";
        public const string HarmonyId = "com.namphan19.diplomacyintrigue";

        public static SubModule Instance { get; private set; }

        /// <summary>False when startup failed; every system checks this before doing work.</summary>
        public static bool Healthy { get; private set; }

        private static bool _crashLoggingInstalled;

        private Harmony _harmony;
        private bool _shownStartupNotice;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Instance = this;

            try
            {
                Log.Initialize();
                InstallCrashLogging();
                Log.Info("SubModule", "OnSubModuleLoad begin. " + DescribeHost());

                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());

                // After Harmony and inside the same try: the Kingdom screen additions are
                // not load-bearing, and ModUI.Install swallows its own failures so a broken
                // prefab patch cannot cost us the campaign systems below it.
                UI.KingdomScreen.ModUI.Install();

                Healthy = true;
                Log.Info("SubModule", "OnSubModuleLoad complete. Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Healthy = false;
                Log.Error("SubModule", "Startup failed - Diplomacy & Intrigue is disabled for this session.", ex);
            }
        }

        /// <summary>
        /// Writes any unhandled exception into our own log before the process dies.
        ///
        /// Added after a startup crash that left nothing readable behind: the game wrote an
        /// 86 MB minidump and a Windows error record naming only a metadata token, while our
        /// log simply stopped at "OnSubModuleLoad complete". Resolving that token by hand took
        /// an hour to learn one method name and no stack. A crash that leaves no message is
        /// the expensive kind, and this is the cheapest possible insurance against it.
        ///
        /// Deliberately logs *every* unhandled exception rather than filtering to our own
        /// assembly: a stack trace that points somewhere else is still the fastest way to
        /// establish that the fault is not ours.
        /// </summary>
        private static void InstallCrashLogging()
        {
            if (_crashLoggingInstalled) return;
            _crashLoggingInstalled = true;

            try
            {
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    try
                    {
                        Log.Error("Crash",
                            "Unhandled exception" + (e.IsTerminating ? " - the process is going down." : "."),
                            e.ExceptionObject as Exception);
                    }
                    catch { /* already on the way out; nothing useful left to do */ }
                };
            }
            catch (Exception ex)
            {
                Log.Error("SubModule", "Could not install the crash logger.", ex);
            }
        }

        /// <summary>
        /// Names the process hosting the game and the modules loaded with it.
        ///
        /// The official launcher does not spawn the game as a child - it calls the starter's
        /// entry point in its own process - so a launcher-hosted session and a direct one are
        /// indistinguishable in a log while behaving differently. Establishing which of the
        /// two produced a crash cost several hours once; one line prevents it.
        /// </summary>
        private static string DescribeHost()
        {
            try
            {
                var host = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                var modules = TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules();
                var names = new System.Text.StringBuilder();
                foreach (var m in modules)
                {
                    if (names.Length > 0) names.Append(',');
                    names.Append(m.Id);
                }
                return "host=" + host + " modules=[" + names + "]";
            }
            catch (Exception ex)
            {
                return "host=unknown (" + ex.GetType().Name + ")";
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            if (_shownStartupNotice) return;
            _shownStartupNotice = true;

            if (Healthy)
                Log.Notify("Diplomacy & Intrigue v" + ModuleVersion + " loaded. Press Ctrl+D on the map.", Colors.Cyan);
            else
                Log.Notify("Diplomacy & Intrigue failed to load - see Documents/Mount and Blade II Bannerlord/DiplomacyIntrigue/Logs.", Colors.Red);
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (!Healthy) return;
            if (!(game.GameType is Campaign)) return;

            try
            {
                // The internal-war index is static and keyed by clan objects; a campaign loaded
                // after another must not inherit its rebels. The load itself rebuilds it.
                Intrigue.InternalWars.ClearIndex();

                var starter = (CampaignGameStarter)gameStarterObject;
                RegisterModels(starter);
                RegisterBehaviors(starter);
                Log.Info("SubModule", "Campaign behaviors registered.");
            }
            catch (Exception ex)
            {
                Log.Error("SubModule", "OnGameStart failed.", ex);
            }
        }

        /// <summary>
        /// Takes the rest of inter-kingdom diplomacy from vanilla. Inventory and reasoning:
        /// docs/design/05-vanilla-override.md.
        ///
        /// Registered unconditionally, even when the diplomacy pillar is switched off in the
        /// settings: each model checks <c>VanillaDiplomacy.Active</c> per call and falls
        /// through to vanilla, so the pillar can be toggled mid-campaign. Deciding here
        /// instead would freeze the choice at load time.
        ///
        /// A later <c>AddModel</c> wins over an earlier one, which is why these replace the
        /// engine's defaults rather than sitting beside them.
        /// </summary>
        private static void RegisterModels(CampaignGameStarter starter)
        {
            starter.AddModel(new ModKingdomDecisionPermissionModel());
            starter.AddModel(new ModDiplomacyModel());
            starter.AddModel(new ModAllianceModel());
            starter.AddModel(new ModTradeAgreementModel());

            // Court intrigue's one model: keeps the two sides of an internal war out of each
            // other's armies. Inert while no internal war runs, so registered unconditionally
            // like the diplomacy models above.
            starter.AddModel(new ModArmyManagementModel());
            Log.Info("SubModule", "Game models registered: kingdom decisions, peace, alliances, "
                                  + "trade agreements, armies.");
        }

        private static void RegisterBehaviors(CampaignGameStarter starter)
        {
            // Order matters: the state behavior must exist before any system reads it.
            starter.AddBehavior(new CoreBehavior());

            if (Settings.Current.EnableDiplomacy)
            {
                starter.AddBehavior(new WarExhaustionBehavior());
                starter.AddBehavior(new ClaimsBehavior());
                starter.AddBehavior(new TreatyBehavior());
                starter.AddBehavior(new CallToArmsBehavior());
                starter.AddBehavior(new AiDiplomacyBehavior());
                starter.AddBehavior(new TelemetryBehavior());
            }

            // Its own toggle, not EnableDiplomacy: turning court intrigue off must not
            // disturb diplomacy, and turning diplomacy off must not freeze every grievance.
            if (Settings.Current.EnableIntrigue)
            {
                starter.AddBehavior(new IntrigueBehavior());
            }

            // Its own toggle again, for the same reason.
            if (Settings.Current.EnableEspionage)
            {
                starter.AddBehavior(new EspionageBehavior());
            }
        }

        /// <summary>
        /// Opens the diplomacy menu on Ctrl+D while on the campaign map.
        ///
        /// Polling input here rather than registering a game hotkey is a deliberate
        /// trade-off: the hotkey system needs a category registered before the game builds
        /// its input maps, and getting that wrong breaks the player's existing bindings.
        /// A guarded poll cannot. Ctrl+D was chosen because D alone is movement and every
        /// unmodified letter worth having is already a screen.
        /// </summary>
        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            if (!Healthy) return;

            try
            {
                if (!Input.IsKeyDown(InputKey.LeftControl) && !Input.IsKeyDown(InputKey.RightControl)) return;
                if (!Input.IsKeyPressed(InputKey.D)) return;

                // Only on the map, and never on top of another dialog or a menu.
                if (!(Game.Current?.GameStateManager?.ActiveState is MapState mapState)) return;
                if (mapState.AtMenu || mapState.MapConversationActive) return;
                if (InformationManager.IsAnyInquiryActive()) return;

                DiplomacyMenu.Open();
            }
            catch (Exception ex)
            {
                Log.Error("SubModule", "Opening the diplomacy menu failed.", ex);
            }
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            Log.Info("SubModule", "Game ended.");
        }
    }
}
