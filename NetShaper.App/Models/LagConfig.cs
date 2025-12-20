namespace NetShaper.App.Models
{
    /// <summary>Lag config: adds fixed delay to packets.</summary>
    public sealed class LagConfig : RuleConfigBase
    {
        private int _delayMs = 100;

        public int DelayMs
        {
            get => _delayMs;
            set => SetField(ref _delayMs, value);
        }
    }
}
