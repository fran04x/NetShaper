using System.ComponentModel;
using System.Windows;
using NetShaper.App.ViewModels;

namespace NetShaper.App.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private ResourceDictionary? _darkColorsDict;
        
        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            
            // Listen for theme changes
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
            {
                ApplyTheme(_viewModel.IsDarkTheme);
            }
        }
        
        private void ApplyTheme(bool isDark)
        {
            var app = Application.Current;
            var mergedDictionaries = app.Resources.MergedDictionaries;
            
            if (isDark)
            {
                // Add dark colors on top (overrides light colors from DefaultTheme.xaml)
                if (_darkColorsDict == null)
                {
                    _darkColorsDict = new ResourceDictionary
                    {
                        Source = new System.Uri("Themes/Colors.Dark.xaml", System.UriKind.Relative)
                    };
                }
                
                if (!mergedDictionaries.Contains(_darkColorsDict))
                {
                    // Add at the end so it overrides colors in DefaultTheme.xaml
                    mergedDictionaries.Add(_darkColorsDict);
                }
            }
            else
            {
                // Remove dark colors overlay, revert to light (DefaultTheme.xaml colors)
                if (_darkColorsDict != null && mergedDictionaries.Contains(_darkColorsDict))
                {
                    mergedDictionaries.Remove(_darkColorsDict);
                }
            }
        }
    }
}

