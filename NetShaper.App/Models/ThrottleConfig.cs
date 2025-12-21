namespace NetShaper.App.Models
{
    /// <summary>Throttle config: rate-limit packets with PPS and timeframe.</summary>
    public sealed class ThrottleConfig : RuleConfigBase
    {
        private int _pps = 10000;
        private int _timeframeMs = 1000;
        private bool _drop = true;
        private int _chance = 100;

        /// <summary>Packets per second limit.</summary>
        public int Pps
        {
            get => _pps;
            set => SetField(ref _pps, value);
        }

        /// <summary>Timeframe in milliseconds for rate calculation.</summary>
        public int TimeframeMs
        {
            get => _timeframeMs;
            set => SetField(ref _timeframeMs, value);
        }

        /// <summary>If true, drop excess packets. If false, delay them.</summary>
        public bool Drop
        {
            get => _drop;
            set => SetField(ref _drop, value);
        }

        /// <summary>Chance to apply throttle (0-100%).</summary>
        public int Chance
        {
            get => _chance;
            set => SetField(ref _chance, value);
        }
    }
}
