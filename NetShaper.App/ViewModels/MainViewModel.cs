using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;
using NetShaper.App.Models;
using NetShaper.App.Services;
using NetShaper.Rules;

namespace NetShaper.App.ViewModels
{
    /// <summary>
    /// Main ViewModel for NetShaper.App.
    /// Handles rule configuration with reactive updates via RebuildAndSwap.
    /// </summary>
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly RulesetBuilder _rulesetBuilder;
        private readonly RulePipeline _pipeline;
        private readonly DispatcherTimer _debounce;
        private RulesConfig _rulesConfig = new();
        private bool _isRunning;
        private bool _isDarkTheme;
        private string _filterText = "";

        /// <summary>
        /// Parameterless constructor for design-time and standalone usage.
        /// Creates internal RulePipeline (not connected to real engine).
        /// </summary>
        public MainViewModel() : this(new RulePipeline())
        {
        }

        public MainViewModel(RulePipeline pipeline)
        {
            _pipeline = pipeline;
            _rulesetBuilder = new RulesetBuilder();
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _debounce.Tick += (s, e) =>
            {
                _debounce.Stop();
                RebuildAndSwap();
            };

            // Initialize commands
            ToggleRunningCommand = new RelayCommand(ToggleRunning);
            ToggleThemeCommand = new RelayCommand(ToggleTheme);
        }

        public RulesConfig RulesConfig
        {
            get => _rulesConfig;
            set
            {
                if (_rulesConfig != value)
                {
                    _rulesConfig = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Engine lifecycle. true = engine started, swaps apply.
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StartStopButtonText));
                    
                    // When starting, apply current config
                    if (_isRunning)
                        RebuildAndSwap();
                }
            }
        }

        /// <summary>
        /// Text shown on START/STOP button.
        /// </summary>
        public string StartStopButtonText => IsRunning ? "STOP" : "START";

        /// <summary>
        /// Current theme mode. true = dark, false = light.
        /// </summary>
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (_isDarkTheme != value)
                {
                    _isDarkTheme = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ThemeIcon));
                }
            }
        }

        /// <summary>
        /// Icon shown on theme toggle button.
        /// Uses Segoe MDL2 Assets: E706 = brightness (sun), E708 = brightness down (moon-like)
        /// </summary>
        public string ThemeIcon => IsDarkTheme ? "\uE706" : "\uE708";

        /// <summary>
        /// Filter text for searching rules.
        /// </summary>
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText != value)
                {
                    _filterText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Command to toggle engine running state.
        /// </summary>
        public ICommand ToggleRunningCommand { get; }

        /// <summary>
        /// Command to toggle dark/light theme.
        /// </summary>
        public ICommand ToggleThemeCommand { get; }

        private void ToggleRunning()
        {
            IsRunning = !IsRunning;
        }

        private void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
        }

        /// <summary>
        /// Call when rule is toggled on/off (checkbox).
        /// Triggers immediate swap if running.
        /// </summary>
        public void OnRuleToggled() => RebuildAndSwap();

        /// <summary>
        /// Call when rule parameter changes (slider/textbox).
        /// Triggers debounced swap (200ms).
        /// </summary>
        public void OnRuleParamChanged() => ScheduleSwap();

        private void ScheduleSwap()
        {
            _debounce.Stop();
            _debounce.Start();
        }

        private void RebuildAndSwap()
        {
            if (!IsRunning)
                return;
            if (!RulesConfig.IsValid())
                return;
            
            var ruleset = _rulesetBuilder.Build(RulesConfig);
            _pipeline.Swap(ruleset);

            // TcpRst oneShot auto-disable: prevent continuous RST injection loops
            if (RulesConfig.TcpRst.Enabled && RulesConfig.TcpRst.OneShot)
                RulesConfig.TcpRst.Enabled = false;
        }

        public void Load(string path)
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<RulesConfig>(json);
            if (loaded != null)
            {
                RulesConfig = loaded;
                OnPropertyChanged(nameof(RulesConfig));
                if (IsRunning)
                    RebuildAndSwap();
            }
        }

        public void Save(string path)
        {
            var json = JsonSerializer.Serialize(RulesConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            // No swap on save
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Simple ICommand implementation for ViewModel commands.
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
