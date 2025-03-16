using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;

namespace SSE.CRA.UI
{
    /// <summary>
    /// Interaction logic for AboutDialog.xaml
    /// </summary>
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            var hl = (Hyperlink)sender;
            try
            {
                Process.Start(new ProcessStartInfo(hl.NavigateUri.ToString()) { UseShellExecute = true });
            }

            catch (Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }
    }
}
