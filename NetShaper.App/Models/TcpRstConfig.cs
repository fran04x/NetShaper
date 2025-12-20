namespace NetShaper.App.Models
{
    /// <summary>TcpRst config: injects TCP RST packets (experimental).</summary>
    public sealed class TcpRstConfig : RuleConfigBase
    {
        private bool _oneShot;

        public bool OneShot
        {
            get => _oneShot;
            set => SetField(ref _oneShot, value);
        }
    }
}
