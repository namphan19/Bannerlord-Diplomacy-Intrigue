using System;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// One comparison row of the mod's own, shaped exactly like the game's
    /// <c>KingdomWarComparableStatVM</c> so the prefab can draw it with TaleWorlds' own
    /// widgets and brushes.
    ///
    /// **Why a copy instead of the real thing.** Appending our rows to the panel's own
    /// <c>Stats</c> list was tried first and does not survive: the list is rebuilt after
    /// UIExtenderEx's hook runs, so the rows vanish without an error anywhere - the mixin
    /// was alive the whole time, as its other bindings kept updating. The choice was then a
    /// third Harmony patch or our own list drawn with their markup, and the second keeps the
    /// patch count where the project wants it.
    /// </summary>
    public sealed class DiplomacyStatVM : ViewModel
    {
        public DiplomacyStatVM(string name, int ourValue, int theirValue, int range,
            string ourColour, string theirColour, string ourHint, string theirHint)
        {
            Name = name ?? string.Empty;
            OurValue = ourValue;
            TheirValue = theirValue;
            OurColor = ourColour;
            TheirColor = theirColour;
            OurPercentage = Percent(ourValue, range);
            TheirPercentage = Percent(theirValue, range);
            OurHint = new BasicTooltipViewModel(() => ourHint);
            TheirHint = new BasicTooltipViewModel(() => theirHint);
        }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public int OurValue { get; }
        [DataSourceProperty] public int TheirValue { get; }
        [DataSourceProperty] public int OurPercentage { get; }
        [DataSourceProperty] public int TheirPercentage { get; }
        [DataSourceProperty] public string OurColor { get; }
        [DataSourceProperty] public string TheirColor { get; }
        [DataSourceProperty] public BasicTooltipViewModel OurHint { get; }
        [DataSourceProperty] public BasicTooltipViewModel TheirHint { get; }

        /// <summary>
        /// A bar cannot show a negative fill, and trust runs -100..+100. Below zero the bar
        /// reads empty and the number beside it still says what it is, which is the honest
        /// way round: the figure is the fact, the bar is the glance.
        /// </summary>
        private static int Percent(int value, int range)
        {
            if (range <= 0) return 0;
            var percent = (int)(value * 100f / range);
            if (percent < 0) return 0;
            return percent > 100 ? 100 : percent;
        }
    }

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
