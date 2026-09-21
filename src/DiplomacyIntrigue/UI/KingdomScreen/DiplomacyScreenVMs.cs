using System;
using DiplomacyIntrigue.Diplomacy;
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

    /// <summary>
    /// One rung of the pact ladder in the "what their court would sign" panel: the name,
    /// the bar of their valuation, where the bar has to reach, the verdict, and the
    /// button that proposes it. The number on the bar is <see cref="AiDiplomacy.PactValue"/>,
    /// the same figure their court decides on - there is no separate figure for the player.
    /// </summary>
    internal sealed class DiPactRungVM : ViewModel
    {
        private readonly System.Action _propose;

        public DiPactRungVM(string name, int value, int threshold, bool canSign,
            int influenceCost, string hint, System.Action propose)
        {
            Name = name;
            BarValue = value < 0 ? 0 : value > 100 ? 100 : value;
            // Green when they would sign, red when they would refuse, grey when the pact
            // is structurally impossible - the bar carries the verdict because text
            // colours come from brushes and cannot be bound.
            BarColor = !canSign ? "#8A7F6DFF"
                : value >= threshold ? "#9AC26AFF" : "#E08070FF";
            DetailText = "valued " + value + " - need " + threshold;
            VerdictText = !canSign ? "cannot sign"
                : value >= threshold ? "would sign" : "refuses";
            CanPropose = canSign && value >= threshold;
            InfluenceCost = influenceCost;
            HasInfluenceCost = influenceCost > 0;
            Hint = new BasicTooltipViewModel(() => hint);
            _propose = propose;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public int BarValue { get; }
        [DataSourceProperty] public string BarColor { get; }
        [DataSourceProperty] public string DetailText { get; }
        [DataSourceProperty] public string VerdictText { get; }
        [DataSourceProperty] public bool CanPropose { get; }
        [DataSourceProperty] public int InfluenceCost { get; }
        [DataSourceProperty] public bool HasInfluenceCost { get; }
        [DataSourceProperty] public BasicTooltipViewModel Hint { get; }

        public void ExecutePropose()
        {
            try
            {
                _propose?.Invoke();
            }
            catch (System.Exception ex)
            {
                Core.Log.Error("UI", "A pact proposal failed.", ex);
            }
        }
    }
}
