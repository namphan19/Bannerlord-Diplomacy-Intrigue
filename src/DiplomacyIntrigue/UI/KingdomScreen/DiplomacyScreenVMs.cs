using System;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// One of the mod's actions, shaped like the game's
    /// <c>KingdomDiplomacyProposalActionItemVM</c> - name, the sentence above the button,
    /// an influence cost and a hover hint - so the prefab draws it as another proposal
    /// button rather than as something of ours.
    ///
    /// The action itself is always a call into <see cref="DiplomacyMenu"/>: one
    /// implementation of each act, two ways to reach it.
    /// </summary>
    public sealed class DiplomacyActionVM : ViewModel
    {
        private readonly Action _action;

        public DiplomacyActionVM(string name, string explanation, int influenceCost,
            bool isEnabled, string hint, Action action)
        {
            Name = name ?? string.Empty;
            Explanation = explanation ?? string.Empty;
            InfluenceCost = influenceCost;
            IsEnabled = isEnabled;
            Hint = new BasicTooltipViewModel(() => hint);
            _action = action;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string Explanation { get; }
        [DataSourceProperty] public int InfluenceCost { get; }
        [DataSourceProperty] public bool IsEnabled { get; }
        [DataSourceProperty] public BasicTooltipViewModel Hint { get; }

        [DataSourceProperty] public bool HasInfluenceCost => InfluenceCost > 0;

        public void ExecuteAction()
        {
            // Nothing thrown from a button may reach the engine: it would come out inside
            // Gauntlet's click handling and take the screen with it.
            try
            {
                _action?.Invoke();
            }
            catch (Exception ex)
            {
                Core.Log.Error("UI", "A Diplomacy tab action failed.", ex);
            }
        }
    }
}
