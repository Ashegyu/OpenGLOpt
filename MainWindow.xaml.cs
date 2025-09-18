using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using OpenGLOpt.ViewModels;

namespace OpenGLOpt
{
    /// <summary>
    /// MVVM-compliant MainWindow with GLWpfControl integration
    /// </summary>
    public partial class MainWindow : Window
    {
        public ParticleRenderViewModel ViewModel { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize ViewModel
            ViewModel = new ParticleRenderViewModel();
            DataContext = ViewModel;
            
            // Setup event handlers for proper cleanup
            Loaded += OnMainWindowLoaded;
            Closing += OnMainWindowClosing;
        }

        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Pass ViewModel to OpenGL Control
            OpenGLControl.DataContext = ViewModel;
            
            // Initialize OpenGL control through ViewModel if needed
            ViewModel.StatusMessage = "OpenGL Ready - GLWpfControl Initialized";
        }

        private void OnMainWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Proper cleanup
            ViewModel?.Dispose();
        }
    }

    /// <summary>
    /// Converter for toggle button text (Start/Stop Rendering)
    /// </summary>
    public class BoolToToggleTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled)
            {
                return isEnabled ? "Stop Rendering" : "Start Rendering";
            }
            return "Toggle Rendering";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
