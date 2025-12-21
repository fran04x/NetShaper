using System;
using System.Globalization;

namespace NetShaper.App.Models
{
    /// <summary>LossPattern config: drops packets by bitmask pattern.</summary>
    public sealed class LossPatternConfig : RuleConfigBase
    {
        private string _patternHex = "0xA5";
        private int _length = 8;

        /// <summary>Hex pattern for loss (e.g. "0xA5").</summary>
        public string PatternHex
        {
            get => _patternHex;
            set
            {
                if (SetField(ref _patternHex, value))
                    OnPropertyChanged(nameof(Mask));
            }
        }

        /// <summary>Pattern length in bits.</summary>
        public int Length
        {
            get => _length;
            set => SetField(ref _length, value);
        }

        /// <summary>Computed mask from hex pattern.</summary>
        public ulong Mask
        {
            get
            {
                var hex = _patternHex?.Trim() ?? "0";
                if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    hex = hex.Substring(2);
                if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var val))
                    return val;
                return 0;
            }
        }
    }
}
