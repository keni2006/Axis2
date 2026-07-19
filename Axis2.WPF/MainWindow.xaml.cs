using System.Windows;
using System.Windows.Controls;
using Axis2.WPF.ViewModels;
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

        private MainViewModel? Vm => DataContext as MainViewModel;

        // ---- Theme (footer Light / Dark segmented control) ----
        private void Theme_Light(object sender, RoutedEventArgs e) => ApplyTheme(false);
        private void Theme_Dark(object sender, RoutedEventArgs e) => ApplyTheme(true);

        private void ApplyTheme(bool dark)
        {
            // WPF-UI themes the Fluent window chrome + native title bar (Mica) …
            ApplicationThemeManager.Apply(dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
            // … and our own palette themes the custom-styled content controls.
            App.SetTheme(dark);
            if (Vm != null) Vm.IsDarkTheme = dark;
        }

        // ---- Compact (dense item-picker) mode ----
        private double _restoreWidth, _restoreHeight;
        private WindowState _restoreState;
        private bool _restoreStored;

        private void Compact_Click(object sender, RoutedEventArgs e)
        {
            var vm = Vm;
            if (vm == null) return;
            SetCompact(!vm.IsCompactMode);
        }

        // Whether a tab (by header) is enabled for compact mode, per the Settings → Compact Panel.
        private static bool IsCompactTabEnabled(string header, MainViewModel vm) => header switch
        {
            "General" => vm.CompactShowGeneral,
            "Item" => vm.CompactShowItem,
            "Spawn" => vm.CompactShowSpawn,
            "Travel" => vm.CompactShowTravel,
            "Misc" => vm.CompactShowMisc,
            "Commands" => vm.CompactShowCommands,
            "Account" => vm.CompactShowAccount,
            _ => false
        };

        private void SetCompact(bool on)
        {
            var vm = Vm;
            if (vm == null) return;
            vm.IsCompactMode = on;

            if (on)
            {
                if (!_restoreStored)
                {
                    _restoreWidth = Width;
                    _restoreHeight = Height;
                    _restoreState = WindowState;
                    _restoreStored = true;
                }

                // Show only the tabs chosen in the Compact Panel; collapse the rest.
                TabItem? first = null;
                foreach (var obj in MainTabControl.Items)
                {
                    if (obj is not TabItem ti) continue;
                    var header = ti.Header?.ToString() ?? string.Empty;
                    bool show = IsCompactTabEnabled(header, vm);
                    ti.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                    if (show && first == null) first = ti;
                }
                // Fallback: if nothing is selected, keep the Item tab available.
                if (first == null)
                {
                    foreach (var obj in MainTabControl.Items)
                    {
                        if (obj is TabItem ti && (ti.Header?.ToString() == "Item"))
                        {
                            ti.Visibility = Visibility.Visible;
                            first = ti;
                            break;
                        }
                    }
                }
                if (first != null) first.IsSelected = true;

                ItemView.SetCompact(true);
                WindowState = WindowState.Normal;
                MinWidth = 430;
                MinHeight = 560;
                Width = 470;
                Height = 650;
            }
            else
            {
                foreach (var obj in MainTabControl.Items)
                    if (obj is TabItem ti) ti.Visibility = Visibility.Visible;

                ItemView.SetCompact(false);
                MinWidth = 1150;
                MinHeight = 650;
                if (_restoreStored)
                {
                    Width = _restoreWidth;
                    Height = _restoreHeight;
                    WindowState = _restoreState;
                    _restoreStored = false;
                }
            }
        }
    }
}
