using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace DiplomacyIntrigue.UI.Negotiation
{
    /// <summary>
    /// The peace table's shell: one GauntletLayer laid over whatever screen is on top -
    /// the Kingdom screen when it is opened from there, the campaign map when an AI
    /// court's offer arrives - with one movie and one view model.
    ///
    /// It is a layer on the current screen, not a screen of its own. The first version
    /// pushed a <c>ScreenBase</c>, and a pushed screen deactivates the one below it: the
    /// Kingdom screen stopped drawing and the table stood alone on black, and because
    /// the layer never claimed input restrictions the cursor was hidden over it. So: add
    /// a layer, restrict input to it, give it focus, and hand focus back on close
    /// (verified live 2026-09-24, over the Kingdom screen and over the map).
    ///
    /// Over the campaign map the table also pauses the campaign, through the active-state
    /// disable request that the game's own Gauntlet assemblies call
    /// (TaleWorlds.MountAndBlade.GauntletUI, SandBox.View) - the inquiry this screen
    /// replaced paused the game, and an offer read while the war ticks on underneath
    /// could be re-priced before the player finished reading it. Verified: campaign time
    /// held while the table was open and moved again after it closed.
    ///
    /// This is the project's one owned Gauntlet surface (the mockup's board 3a/3b
    /// exception - a budget that updates as terms are picked cannot live in an inquiry).
    /// The movie is <c>DiPeaceTable</c> from <c>module/DiplomacyIntrigue/GUI/Prefabs/</c>.
    /// If the table cannot open, <see cref="Open"/> throws after undoing what it did, and
    /// the callers fall back to the old inquiries.
    /// </summary>
    internal sealed class PeaceTablePopup
    {
        /// <summary>
        /// The layer's order within its host screen: high enough to draw over the Kingdom
        /// screen's and the map screen's own layers. Inquiries live on a global layer,
        /// not on the host, so a confirmation a button raises is not affected by this.
        /// </summary>
        private const int LayerOrder = 400;

        private readonly ScreenBase _host;
        private readonly PeaceTableVM _vm;
        private readonly ScreenLayer _previousFocus;
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movie;
        private bool _pausedCampaign;
        private bool _closed;

        private PeaceTablePopup(ScreenBase host, PeaceTableVM vm)
        {
            _host = host;
            _vm = vm;
            _previousFocus = ScreenManager.FocusedLayer;
        }

        // ----- the two doors in -------------------------------------------------

        /// <summary>
        /// The editable table: the winner prices what to take, the loser prices what to
        /// give. <paramref name="weAreWinner"/> decides which side of it we sit on.
        /// </summary>
        internal static void ShowDemand(ModState state, WarRecord war, Kingdom us,
            Kingdom them, bool weAreWinner, Action<PeaceTerms> onOffer, Action onWhitePeace)
        {
            PeaceTablePopup popup = null;
            var cb = new PeaceTableVM.Callbacks
            {
                OnOffer = onOffer,
                OnWhitePeace = onWhitePeace,
                OnClose = () => popup?.Close(),
            };
            popup = Open(new PeaceTableVM(state, war, us, them, weAreWinner, null, cb));
        }

        /// <summary>Their package, read only: accept it or refuse it.</summary>
        internal static void ShowIncoming(ModState state, WarRecord war, Kingdom us,
            Kingdom them, PeaceTerms terms, Action onAccept, Action onRefuse)
        {
            PeaceTablePopup popup = null;
            var cb = new PeaceTableVM.Callbacks
            {
                OnAccept = onAccept,
                OnRefuse = onRefuse,
                OnClose = () => popup?.Close(),
            };
            popup = Open(new PeaceTableVM(state, war, us, them, terms.Winner == us, terms, cb));
        }

        // ----- open and close -----------------------------------------------------

        private static PeaceTablePopup Open(PeaceTableVM vm)
        {
            var host = ScreenManager.TopScreen;
            if (host == null)
                throw new InvalidOperationException("There is no screen to open the peace table over.");

            var popup = new PeaceTablePopup(host, vm);
            try
            {
                popup.Attach();
                return popup;
            }
            catch (Exception ex)
            {
                // Half an attach must not survive: a stray layer holding input focus, or a
                // pause request nobody lifts, would outlive the fallback inquiry.
                Log.Error("UI", "Could not open the peace table.", ex);
                popup.Close();
                throw;
            }
        }

        private void Attach()
        {
            // shouldClear false: the layer draws over the screen below instead of wiping
            // the frame first - the other half of the black background.
            _layer = new GauntletLayer("DiPeaceTableLayer", LayerOrder, false);
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            _movie = _layer.LoadMovie("DiPeaceTable", _vm);
            _host.AddLayer(_layer);
            _layer.IsFocusLayer = true;
            ScreenManager.TrySetFocus(_layer);

            // Only the map ticks the campaign. Over the Kingdom screen nothing moves, and a
            // request there could be orphaned if that screen closed under the table.
            var states = Game.Current?.GameStateManager;
            if (states != null && states.ActiveState is MapState)
            {
                states.RegisterActiveStateDisableRequest(this);
                _pausedCampaign = true;
            }
        }

        /// <summary>
        /// Idempotent, and each step is guarded on its own: a failure removing the layer
        /// must not leave the campaign paused, and a failure lifting the pause must not
        /// leave the layer holding input.
        /// </summary>
        private void Close()
        {
            if (_closed) return;
            _closed = true;

            if (_pausedCampaign)
            {
                try { Game.Current?.GameStateManager?.UnregisterActiveStateDisableRequest(this); }
                catch (Exception ex) { Log.Error("UI", "Could not resume the campaign after the peace table.", ex); }
                _pausedCampaign = false;
            }

            if (_layer != null)
            {
                try
                {
                    _layer.InputRestrictions.ResetInputRestrictions();
                    _layer.IsFocusLayer = false;
                    ScreenManager.TryLoseFocus(_layer);
                    if (_movie != null) _layer.ReleaseMovie(_movie);
                    if (_host.HasLayer(_layer)) _host.RemoveLayer(_layer);
                }
                catch (Exception ex)
                {
                    Log.Error("UI", "Could not close the peace table.", ex);
                }
                _layer = null;
                _movie = null;
            }

            // Focus goes back to whoever had it - the Kingdom screen's own layer, or a
            // table still open beneath this one when two offers arrived in one week.
            try
            {
                if (_previousFocus != null && _host.HasLayer(_previousFocus))
                    ScreenManager.TrySetFocus(_previousFocus);
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Could not return focus after the peace table.", ex);
            }

            try { _vm.OnFinalize(); }
            catch (Exception ex) { Log.Error("UI", "Finalizing the peace table failed.", ex); }
        }
    }
}
