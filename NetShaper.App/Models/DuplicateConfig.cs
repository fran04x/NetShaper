namespace NetShaper.App.Models
{
    /// <summary>Duplicate config: send packet N times with chance.</summary>
    public sealed class DuplicateConfig : RuleConfigBase
    {
        private int _count = 2;
        private int _chance = 100;

        /// <summary>Number of copies to send.</summary>
        public int Count
        {
            get => _count;
            set => SetField(ref _count, value);
        }

        /// <summary>Chance to duplicate packet (0-100%).</summary>
        public int Chance
        {
            get => _chance;
            set => SetField(ref _chance, value);
        }
    }
}
