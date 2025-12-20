namespace NetShaper.App.Models
{
    /// <summary>Jitter config: adds random delay variance.</summary>
    public sealed class JitterConfig : RuleConfigBase
    {
        private int _minMs = 0;
        private int _maxMs = 50;

        public int MinMs
        {
            get => _minMs;
            set => SetField(ref _minMs, value);
        }

        public int MaxMs
        {
            get => _maxMs;
            set => SetField(ref _maxMs, value);
        }
    }
}
