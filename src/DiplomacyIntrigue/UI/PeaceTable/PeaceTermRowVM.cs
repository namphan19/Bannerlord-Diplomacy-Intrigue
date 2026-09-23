using System;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.UI.Negotiation
{
    /// <summary>
    /// One line of the peace table: a demand (or a concession, or a term of theirs), its
    /// one-line why, and its price against the war-score budget - the mockup's row of a
    /// checkbox, a name over a description, and a price at the right edge.
    ///
    /// Nothing here decides anything: the price is <see cref="Diplomacy.PeaceTable.CostOf"/>
    /// on a trial package, what may be asked is
    /// <see cref="Diplomacy.PeaceTable.IsDemandable"/> on the same package, and what
    /// conflicts with what is <see cref="PeaceTerms.AreExclusive"/>. The screen only
    /// ticks boxes and re-reads.
    /// </summary>
    internal sealed class PeaceTermRowVM : ViewModel
    {
        // The mockup's palette: gold is chosen, tan is available, grey is locked out.
        internal static readonly Color OnNameColor = Color.ConvertStringToColor("#E6C87FFF");
        internal static readonly Color OffNameColor = Color.ConvertStringToColor("#DDD0B8FF");
        internal static readonly Color LockedColor = Color.ConvertStringToColor("#6F6250FF");
        internal static readonly Color MutedColor = Color.ConvertStringToColor("#A89878FF");
        internal static readonly Color DimColor = Color.ConvertStringToColor("#5F5342FF");

        private readonly Action _toggle;
        private bool _isOn;
        private bool _isOff = true;
        private Color _nameColor;
        private Color _descColor;
        private Color _priceColor;

        public PeaceTermRowVM(PeaceTermKind kind, Settlement fief, string name, string desc,
            string price, bool enabled, string disabledReason, Action toggle)
        {
            Kind = kind;
            Fief = fief;
            Name = name;
            Desc = enabled ? desc : disabledReason;
            Price = enabled ? price : "-";
            Enabled = enabled;
            NameColor = enabled ? OffNameColor : LockedColor;
            DescColor = enabled ? MutedColor : DimColor;
            PriceColor = enabled ? MutedColor : DimColor;
            _toggle = toggle;
        }

        [DataSourceProperty] public PeaceTermKind Kind { get; }
        [DataSourceProperty] public Settlement Fief { get; }
        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string Desc { get; }
        [DataSourceProperty] public string Price { get; }
        [DataSourceProperty] public bool Enabled { get; }

        [DataSourceProperty]
        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (value == _isOn) return;
                _isOn = value;
                IsOff = !value;
                NameColor = !Enabled ? LockedColor : value ? OnNameColor : OffNameColor;
                PriceColor = !Enabled ? DimColor : value ? OnNameColor : MutedColor;
                OnPropertyChangedWithValue(value, nameof(IsOn));
            }
        }

        /// <summary>The empty box's flag - a binding cannot negate, so both are exposed.</summary>
        [DataSourceProperty]
        public bool IsOff
        {
            get => _isOff;
            set
            {
                if (value == _isOff) return;
                _isOff = value;
                OnPropertyChangedWithValue(value, nameof(IsOff));
            }
        }

        [DataSourceProperty]
        public Color NameColor
        {
            get => _nameColor;
            set
            {
                if (value.Equals(_nameColor)) return;
                _nameColor = value;
                OnPropertyChangedWithValue(value, nameof(NameColor));
            }
        }

        [DataSourceProperty]
        public Color DescColor
        {
            get => _descColor;
            set
            {
                if (value.Equals(_descColor)) return;
                _descColor = value;
                OnPropertyChangedWithValue(value, nameof(DescColor));
            }
        }

        [DataSourceProperty]
        public Color PriceColor
        {
            get => _priceColor;
            set
            {
                if (value.Equals(_priceColor)) return;
                _priceColor = value;
                OnPropertyChangedWithValue(value, nameof(PriceColor));
            }
        }

        public void ExecuteToggle()
        {
            try
            {
                _toggle?.Invoke();
            }
            catch (Exception ex)
            {
                Core.Log.Error("UI", "Ticking a peace-table row failed.", ex);
            }
        }
    }
}
