namespace NetShaper.App.Models
{
    /// <summary>Drop config: drops matching packets with configurable chance.</summary>
    public sealed class DropConfig : RuleConfigBase
    {
        private int _chance = 100;

        /// <summary>Chance to drop packet (0-100%).</summary>
        public int Chance
        {
            get => _chance;
            set => SetField(ref _chance, value);
        }
    }
}
