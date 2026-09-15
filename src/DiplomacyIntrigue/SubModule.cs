using System;
using System.Reflection;
using HarmonyLib;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
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

        private Harmony _harmony;
        private bool _shownStartupNotice;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Instance = this;

            try
            {
                Log.Initialize();
                Log.Info("SubModule", "OnSubModuleLoad begin.");

                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());

                Healthy = true;
                Log.Info("SubModule", "OnSubModuleLoad complete. Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Healthy = false;
                Log.Error("SubModule", "Startup failed - Diplomacy & Intrigue is disabled for this session.", ex);
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
                var starter = (CampaignGameStarter)gameStarterObject;
                RegisterBehaviors(starter);
                Log.Info("SubModule", "Campaign behaviors registered.");
            }
            catch (Exception ex)
            {
                Log.Error("SubModule", "OnGameStart failed.", ex);
            }
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
