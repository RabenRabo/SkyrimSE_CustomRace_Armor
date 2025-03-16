using SSE.CRA.VM;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SSE.CRA.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region ctors
        public MainWindow()
        {
            InitializeComponent();
            MainViewModel vm = (MainViewModel)DataContext;
            vm.ConsoleTextCleared += Vm_ConsoleTextCleared;
            vm.ConsoleTextChanged += Vm_ConsoleTextChanged;
        }
        #endregion

        #region event handlers
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ((MainViewModel)DataContext).Initialise();
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ((MainViewModel)DataContext).Uninitialise();
        }
        private void Vm_ConsoleTextChanged(string msg, bool newline)
        {
            txtConsole.AppendText(msg);
            if(newline) txtConsole.AppendText(Environment.NewLine);
        }
        private void Vm_ConsoleTextCleared(object? sender, EventArgs e)
        {
            txtConsole.Text = "";
        }
        private void DataGrid_LostFocus(object sender, RoutedEventArgs e)
        {
            var dg = (DataGrid)sender;
            if(ReferenceEquals(dg.SelectedValue , CollectionView.NewItemPlaceholder))
            {
                dg.SelectedValue = null;
            }
        }
        #endregion
    }
}