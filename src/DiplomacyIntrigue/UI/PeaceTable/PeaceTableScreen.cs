using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace DiplomacyIntrigue.UI.Negotiation
{
    /// <summary>
    /// The peace table's screen shell: one GauntletLayer, one movie, one view model.
    ///
    /// This is the project's one owned screen (the mockup's board 3a/3b exception - a
    /// budget that updates as terms are picked cannot live in an inquiry). Everything
    /// else in the mod stays inside the game's own screens.
    ///
    /// The movie is <c>DiPeaceTable</c> from <c>module/DiplomacyIntrigue/GUI/Prefabs/</c>;
    /// modules' GUI folders are collected without any SubModule.xml entry. A failure to
    /// push the screen leaves the game exactly as it was - the callers wrap
    /// <see cref="ShowDemand"/> / <see cref="ShowIncoming"/> in a try/catch.
    /// </summary>
    internal sealed class PeaceTableScreen : ScreenBase
    {
        private readonly PeaceTableVM _vm;
        private GauntletLayer _layer;

        private PeaceTableScreen(PeaceTableVM vm)
        {
            _vm = vm;
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _layer = new GauntletLayer("DiPeaceTableLayer", 210, true);
            _layer.LoadMovie("DiPeaceTable", _vm);
            AddLayer(_layer);
            MouseVisible = true;
        }

        protected override void OnFinalize()
        {
            if (_layer != null)
            {
                RemoveLayer(_layer);
                _layer = null;
            }
            base.OnFinalize();
        }

        // ----- the two doors in -------------------------------------------------

        /// <summary>
        /// The editable table: the winner prices what to take, the loser prices what to
        /// give. <paramref name="weAreWinner"/> decides which side of it we sit on.
        /// </summary>
        internal static void ShowDemand(ModState state, WarRecord war, Kingdom us,
            Kingdom them, bool weAreWinner, Action<PeaceTerms> onOffer, Action onWhitePeace)
        {
            var cb = new PeaceTableVM.Callbacks
            {
                OnOffer = onOffer,
                OnWhitePeace = onWhitePeace,
                OnClose = Pop,
            };
            var vm = new PeaceTableVM(state, war, us, them, weAreWinner, null, cb);
            Push(vm);
        }

        /// <summary>Their package, read only: accept it or refuse it.</summary>
        internal static void ShowIncoming(ModState state, WarRecord war, Kingdom us,
            Kingdom them, PeaceTerms terms, Action onAccept, Action onRefuse)
        {
            var cb = new PeaceTableVM.Callbacks
            {
                OnAccept = onAccept,
                OnRefuse = onRefuse,
                OnClose = Pop,
            };
            var vm = new PeaceTableVM(state, war, us, them, terms.Winner == us, terms, cb);
            Push(vm);
        }

        private static void Push(PeaceTableVM vm)
        {
            try
            {
                ScreenManager.PushScreen(new PeaceTableScreen(vm));
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Could not open the peace table.", ex);
                throw;
            }
        }

        private static void Pop()
        {
            try
            {
                ScreenManager.PopScreen();
            }
            catch (Exception ex)
            {
                Log.Error("UI", "Could not close the peace table.", ex);
            }
        }
    }
}
