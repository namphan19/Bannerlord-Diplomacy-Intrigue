using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.KingdomScreen
{
    /// <summary>
    /// One label/value row of the mod's block in the Diplomacy tab.
    ///
    /// A list of these, rather than a fixed set of bound properties, is what keeps the
    /// prefab patch small: the panel gets one <c>ListPanel</c> with one item template, and
    /// how many lines there are and what they say is decided in C# where it can be tested
    /// and where a game update cannot reach it.
    /// </summary>
    public sealed class DiplomacyLineVM : ViewModel
    {
        private string _label;
        private string _value;

        public DiplomacyLineVM(string label, string value)
        {
            _label = label ?? string.Empty;
            _value = value ?? string.Empty;
        }

        [DataSourceProperty]
        public string Label
        {
            get => _label;
            set
            {
                if (value == _label) return;
                _label = value;
                OnPropertyChangedWithValue(value, nameof(Label));
            }
        }

        [DataSourceProperty]
        public string Value
        {
            get => _value;
            set
            {
                if (value == _value) return;
                _value = value;
                OnPropertyChangedWithValue(value, nameof(Value));
            }
        }
    }
}
