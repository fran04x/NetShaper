namespace NetShaper.App.Models
{
    /// <summary>AckDelay config: delays ACK packets specifically.</summary>
    public sealed class AckDelayConfig : RuleConfigBase
    {
        private int _delayMs = 50;

        public int DelayMs
        {
            get => _delayMs;
            set => SetField(ref _delayMs, value);
        }
    }
}
