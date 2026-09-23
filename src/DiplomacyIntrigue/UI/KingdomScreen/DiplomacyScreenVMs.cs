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
    /// The label carries the mockup's kind colours (green is good for us, red is bad for
    /// us, gold is a threshold): a text brush colour binds per widget, a button
    /// background does not, so the meaning lives on the label.
    ///
    /// The action itself is always a call into <see cref="DiplomacyMenu"/>: one
    /// implementation of each act, two ways to reach it.
    /// </summary>
    public sealed class DiplomacyActionVM : ViewModel
    {
        /// <summary>Label colours, one per action kind (the mockup's palette).</summary>
        internal static readonly Color PositiveText = Color.ConvertStringToColor("#E2EEC9FF");
        internal static readonly Color GoldText = Color.ConvertStringToColor("#E6C87FFF");
        internal static readonly Color PlainText = Color.ConvertStringToColor("#E0CFA8FF");
        internal static readonly Color DangerText = Color.ConvertStringToColor("#E8A99CFF");
        internal static readonly Color BlockedText = Color.ConvertStringToColor("#6F6250FF");

        private readonly Action _action;

        public DiplomacyActionVM(string name, string explanation, int influenceCost,
            bool isEnabled, string hint, Action action, Color? labelColor = null)
            : this(name, explanation, influenceCost, string.Empty, isEnabled, hint, action, labelColor)
        {
        }

        /// <param name="costText">
        /// A cost that is not a single figure ("25 - 60" across three rungs). When set it
        /// replaces the influence figure and icon; the hint still carries the breakdown.
        /// </param>
        public DiplomacyActionVM(string name, string explanation, int influenceCost,
            string costText, bool isEnabled, string hint, Action action,
            Color? labelColor = null)
        {
            Name = name ?? string.Empty;
            Explanation = explanation ?? string.Empty;
            InfluenceCost = influenceCost;
            CostText = costText ?? string.Empty;
            IsEnabled = isEnabled;
            Hint = new BasicTooltipViewModel(() => hint);
            LabelColor = labelColor ?? (isEnabled ? PlainText : BlockedText);
            _action = action;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string Explanation { get; }
        [DataSourceProperty] public int InfluenceCost { get; }
        [DataSourceProperty] public string CostText { get; }
        [DataSourceProperty] public bool IsEnabled { get; }
        [DataSourceProperty] public BasicTooltipViewModel Hint { get; }
        [DataSourceProperty] public Color LabelColor { get; }

        [DataSourceProperty] public bool HasInfluenceCost => InfluenceCost > 0 && CostText.Length == 0;

        [DataSourceProperty] public bool HasCostText => CostText.Length > 0;

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
    /// the bar of their valuation with the gold mark the bar has to clear, the verdict
    /// badge, and the button that proposes it. The number on the bar is
    /// <see cref="AiDiplomacy.PactValue"/>, the same figure their court decides on -
    /// there is no separate figure for the player.
    /// </summary>
    internal sealed class DiPactRungVM : ViewModel
    {
        /// <summary>Bar width in the prefab; the threshold mark is placed in these pixels.</summary>
        internal const int BarWidth = 170;

        private static readonly Color LitName = Color.ConvertStringToColor("#E0CFA8FF");
        private static readonly Color DimName = Color.ConvertStringToColor("#8A7F6DFF");
        private static readonly Color RefuseText = Color.ConvertStringToColor("#E08070FF");

        private readonly System.Action _propose;

        public DiPactRungVM(string name, int value, int threshold, bool canSign,
            int influenceCost, string hint, System.Action propose)
        {
            var clears = canSign && value >= threshold;
            Name = name;
            NameColor = clears ? LitName : DimName;
            BarValue = value < 0 ? 0 : value > 100 ? 100 : value;
            // Green when they would sign, brown-red when they would refuse, grey when the
            // pact is structurally impossible - the mockup's palette, carried by the bar.
            BarColor = !canSign ? "#8A7F6DFF" : clears ? "#4D7F52FF" : "#6B4A3AFF";
            // The gold mark is the threshold itself: the one line on the bar that means
            // "this is what each treaty has to clear".
            var offset = (int)(Math.Max(0, Math.Min(100, threshold)) / 100f * BarWidth) - 1;
            NeedPixelOffset = offset < 0 ? 0 : offset > BarWidth - 2 ? BarWidth - 2 : offset;
            DetailText = "valued " + value + " / need " + threshold;
            VerdictText = !canSign ? "Cannot sign" : clears ? "Would sign" : "Refuses";
            VerdictColor = !canSign ? DimName : clears ? LitName : RefuseText;
            // A tinted plate behind the verdict words: a brush border colour cannot be
            // bound, so the badge is a soft fill in the verdict's own hue.
            VerdictBackColor = !canSign ? Color.ConvertStringToColor("#8A7F6D55")
                : clears ? Color.ConvertStringToColor("#9AC26A55")
                    : Color.ConvertStringToColor("#E0807055");
            CanPropose = clears;
            InfluenceCost = influenceCost;
            HasInfluenceCost = influenceCost > 0;
            Hint = new BasicTooltipViewModel(() => hint);
            _propose = propose;
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public Color NameColor { get; }
        [DataSourceProperty] public int BarValue { get; }
        [DataSourceProperty] public string BarColor { get; }
        [DataSourceProperty] public int NeedPixelOffset { get; }
        [DataSourceProperty] public string DetailText { get; }
        [DataSourceProperty] public string VerdictText { get; }
        [DataSourceProperty] public Color VerdictColor { get; }
        [DataSourceProperty] public Color VerdictBackColor { get; }
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
            catch (Exception ex)
            {
                Core.Log.Error("UI", "A pact proposal failed.", ex);
            }
        }
    }
}
