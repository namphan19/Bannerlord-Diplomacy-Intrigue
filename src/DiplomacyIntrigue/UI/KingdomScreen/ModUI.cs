using System;
using System.Reflection;
using Bannerlord.UIExtenderEx;
using DiplomacyIntrigue.Core;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// Installs the mod's additions to the game's own Kingdom screen.
    ///
    /// **Why UIExtenderEx and not a screen of our own.** The lead asked for the mod's
    /// diplomacy to live in the Kingdom screen's Diplomacy tab rather than behind Ctrl+D,
    /// and the cheapest honest way to do that is to extend what is already there: a mixin
    /// adds properties to the game's view models, and a prefab patch adds widgets bound to
    /// them. No Harmony patch, no screen to own, and the vanilla tab keeps working if our
    /// part fails.
    ///
    /// **What breaks when the game updates.** A prefab patch is matched by XPath against
    /// TaleWorlds' own XML, so a rearranged panel makes the patch miss. UIExtenderEx logs
    /// that and carries on; the tab renders without our block rather than not at all. That
    /// is the whole reason the work is split into several small patches instead of one
    /// replacement of the panel: each can miss on its own.
    /// </summary>
    internal static class ModUI
    {
        private static UIExtender _extender;

        internal static bool Installed { get; private set; }

        internal static void Install()
        {
            if (Installed) return;

            try
            {
                _extender = UIExtender.Create(SubModule.ModuleId);
                _extender.Register(Assembly.GetExecutingAssembly());
                _extender.Enable();

                Installed = true;
                Log.Info("UI", "Kingdom screen extensions registered.");
            }
            catch (Exception ex)
            {
                // Never fatal. The Ctrl+D menu remains, and so does the vanilla screen.
                Installed = false;
                Log.Error("UI", "Kingdom screen extensions failed to install; "
                                + "the vanilla Diplomacy tab is untouched and Ctrl+D still works.", ex);
            }
        }
    }
}
