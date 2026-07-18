using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Axis2.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            bool dark = !App.IsDarkTheme;
            // WPF-UI themes the Fluent window chrome + native title bar (Mica) …
            ApplicationThemeManager.Apply(dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
            // … and our own palette themes the custom-styled content controls.
            App.SetTheme(dark);
            if (sender is System.Windows.Controls.Button b)
                b.Content = dark ? "☀ Light" : "🌙 Dark";
        }
    }
}
