namespace NetShaper.App.Models
{
    /// <summary>SynDrop config: drops TCP SYN packets with configurable chance.</summary>
    public sealed class SynDropConfig : RuleConfigBase
    {
        private int _chance = 100;

        /// <summary>Chance to drop SYN packet (0-100%).</summary>
        public int Chance
        {
            get => _chance;
            set => SetField(ref _chance, value);
        }
    }
}
