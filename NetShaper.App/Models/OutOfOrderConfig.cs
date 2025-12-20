namespace NetShaper.App.Models
{
    /// <summary>OutOfOrder config: simulates packet reordering via random delay.</summary>
    public sealed class OutOfOrderConfig : RuleConfigBase
    {
        private int _maxDelayMs = 50;

        public int MaxDelayMs
        {
            get => _maxDelayMs;
            set => SetField(ref _maxDelayMs, value);
        }
    }
}
