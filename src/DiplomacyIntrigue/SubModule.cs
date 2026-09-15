using System;
using System.Reflection;
using HarmonyLib;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

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
                Log.Notify("Diplomacy & Intrigue v" + ModuleVersion + " loaded.", Colors.Cyan);
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
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            Log.Info("SubModule", "Game ended.");
        }
    }
}
