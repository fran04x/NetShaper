namespace NetShaper.App.Models
{
    /// <summary>LossPattern config: drops packets by bitmask pattern.</summary>
    public sealed class LossPatternConfig : RuleConfigBase
    {
        private ulong _mask = 0;
        private int _length = 1;

        public ulong Mask
        {
            get => _mask;
            set => SetField(ref _mask, value);
        }

        public int Length
        {
            get => _length;
            set => SetField(ref _length, value);
        }
    }
}
