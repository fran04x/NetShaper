using System.Windows.Input;

namespace NetShaper.App.Models
{
    /// <summary>TcpRst config: injects TCP RST packets.</summary>
    public sealed class TcpRstConfig : RuleConfigBase
    {
        private int _chance = 100;
        private bool _rstTriggered;

        /// <summary>Chance to inject RST (0-100%).</summary>
        public int Chance
        {
            get => _chance;
            set => SetField(ref _chance, value);
        }

        /// <summary>Visual feedback flag - true briefly when RST is triggered.</summary>
        public bool RstTriggered
        {
            get => _rstTriggered;
            private set => SetField(ref _rstTriggered, value);
        }

        /// <summary>Command to trigger RST on next packet.</summary>
        public ICommand TriggerRstCommand { get; }

        public TcpRstConfig()
        {
            TriggerRstCommand = new RelayCommand(TriggerRst, () => Enabled);
        }

        private void TriggerRst()
        {
            // Set visual feedback
            RstTriggered = true;
            
            // Reset after short delay
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                async () =>
                {
                    await System.Threading.Tasks.Task.Delay(300);
                    RstTriggered = false;
                });
        }
    }

    /// <summary>Simple relay command implementation.</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly System.Action _execute;
        private readonly System.Func<bool>? _canExecute;

        public RelayCommand(System.Action execute, System.Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event System.EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }
}
