namespace NetShaper.App.Models
{
    /// <summary>Throttle config: rate-limit by packets per second (PPS).</summary>
    public sealed class ThrottleConfig : RuleConfigBase
    {
        private int _pps = 1000;

        public int Pps
        {
            get => _pps;
            set => SetField(ref _pps, value);
        }
    }
}
