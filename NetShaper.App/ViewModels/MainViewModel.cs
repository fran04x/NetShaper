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
        private string _filterText = "ip and (tcp or udp)";

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
                    OnPropertyChanged(nameof(ThemeIconBrush));
                }
            }
        }

        /// <summary>
        /// Icon shown on theme toggle button.
        /// Uses Segoe MDL2 Assets: E706 = brightness (sun), E708 = brightness down (moon-like)
        /// </summary>
        public string ThemeIcon => IsDarkTheme ? "\uE706" : "\uE708";

        /// <summary>
        /// Icon color: light in dark mode, dark in light mode.
        /// </summary>
        public System.Windows.Media.SolidColorBrush ThemeIconBrush => 
            IsDarkTheme 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(242, 243, 245)) // Light color
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 31, 34));   // Dark color

        /// <summary>
        /// Filter text for WinDivert packet capture.
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
                    OnPropertyChanged(nameof(IsFilterValid));
                }
            }
        }

        /// <summary>
        /// True if FilterText has valid syntax (balanced parentheses, non-empty).
        /// </summary>
        public bool IsFilterValid => ValidateFilterSyntax(FilterText);

        /// <summary>
        /// Valid WinDivert filter keywords.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> ValidKeywords = new(System.StringComparer.OrdinalIgnoreCase)
        {
            // Boolean literals
            "true", "false",
            
            // Logical operators
            "and", "or", "not",
            
            // Protocol flags (boolean keywords)
            "ip", "ipv6", "icmp", "icmpv6", "tcp", "udp",
            
            // Packet flags
            "inbound", "outbound", "loopback", "impostor", "fragment", "reassembled",
            
            // IP fields
            "ip.HdrLength", "ip.TOS", "ip.Length", "ip.Id", "ip.DF", "ip.MF",
            "ip.FragOff", "ip.TTL", "ip.Protocol", "ip.Checksum",
            "ip.SrcAddr", "ip.DstAddr",
            
            // IPv6 fields
            "ipv6.TrafficClass", "ipv6.FlowLabel", "ipv6.Length", "ipv6.NextHdr", "ipv6.HopLimit",
            "ipv6.SrcAddr", "ipv6.DstAddr",
            
            // ICMP fields
            "icmp.Type", "icmp.Code", "icmp.Checksum", "icmp.Body",
            "icmpv6.Type", "icmpv6.Code", "icmpv6.Checksum", "icmpv6.Body",
            
            // TCP fields
            "tcp.SrcPort", "tcp.DstPort", "tcp.SeqNum", "tcp.AckNum",
            "tcp.HdrLength", "tcp.Urg", "tcp.Ack", "tcp.Psh", "tcp.Rst", "tcp.Syn", "tcp.Fin",
            "tcp.Window", "tcp.Checksum", "tcp.UrgPtr", "tcp.PayloadLength",
            
            // UDP fields
            "udp.SrcPort", "udp.DstPort", "udp.Length", "udp.Checksum", "udp.PayloadLength",
            
            // Address flags
            "localAddr", "remoteAddr", "localPort", "remotePort",
            
            // Interface
            "ifIdx", "subIfIdx",
            
            // Event (for FLOW/SOCKET layers)
            "event", "layer", "priority", "processId"
        };

        /// <summary>
        /// Valid WinDivert comparison operators.
        /// </summary>
        private static readonly string[] ValidOperators = { "==", "!=", "<=", ">=", "<", ">" };

        private static bool ValidateFilterSyntax(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return false;
            
            // Check balanced parentheses
            int depth = 0;
            foreach (char c in filter)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;
                if (depth < 0) return false;
            }
            if (depth != 0) return false;
            
            // Tokenize and validate keywords
            var tokens = filter
                .Replace("(", " ")
                .Replace(")", " ")
                .Replace("==", " == ")
                .Replace("!=", " != ")
                .Replace("<=", " <= ")
                .Replace(">=", " >= ")
                .Replace("&&", " and ")
                .Replace("||", " or ")
                .Replace("!", " not ")
                .Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var token in tokens)
            {
                // Skip operators
                if (System.Array.Exists(ValidOperators, op => op == token))
                    continue;
                
                // Skip numeric values (ports, addresses)
                if (IsNumericOrAddress(token))
                    continue;
                
                // Must be a valid keyword
                if (!ValidKeywords.Contains(token))
                    return false;
            }
            
            return true;
        }
        
        private static bool IsNumericOrAddress(string token)
        {
            // Numeric (port, protocol number)
            if (int.TryParse(token, out _))
                return true;
            
            // IPv4 address (e.g., 127.0.0.1)
            if (System.Net.IPAddress.TryParse(token, out _))
                return true;
            
            // Hex value (e.g., 0x1234)
            if (token.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return true;
            
            return false;
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

            // TcpRst RstTriggered auto-disable: prevent continuous RST injection loops
            if (RulesConfig.TcpRst.Enabled && RulesConfig.TcpRst.RstTriggered)
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
