using System.Diagnostics;
using System.Windows.Navigation;

namespace Axis2.WPF.Views
{
    public partial class AboutTabView : System.Windows.Controls.UserControl
    {
        public AboutTabView()
        {
            InitializeComponent();
        }

        // Open the GitHub link in the default browser.
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch { /* no browser available */ }
            e.Handled = true;
        }
    }
}
