using SSE.CRA.VM;
using System.Collections;
using System.Windows;

namespace SSE.CRA.UI
{
    /// <summary>
    /// Interaction logic for CheckListDialog.xaml
    /// </summary>
    public partial class CheckListDialog : Window
    {
        #region properties
        public IEnumerable? ItemsSource
        {
            get => ((CheckListDialogViewModel)DataContext).ItemsSource;
            set => ((CheckListDialogViewModel)DataContext).ItemsSource = value;
        }
        public IEnumerable SelectedItems
        {
            get => ((CheckListDialogViewModel)DataContext).SelectedItems;
        }
        public string ConfirmText
        {
            get => ((CheckListDialogViewModel)DataContext).ConfirmText;
            set => ((CheckListDialogViewModel)DataContext).ConfirmText = value;
        }
        public Func<object,string>? ItemNameGetter
        {
            get => ((CheckListDialogViewModel)DataContext).ItemNameGetter;
            set => ((CheckListDialogViewModel)DataContext).ItemNameGetter = value;
        }
        public Func<object, bool>? ItemPreselector
        {
            get => ((CheckListDialogViewModel)DataContext).ItemPreselecter;
            set => ((CheckListDialogViewModel)DataContext).ItemPreselecter = value;
        }
        #endregion

        #region ctors
        public CheckListDialog()
        {
            InitializeComponent();
            ((CheckListDialogViewModel)DataContext).DialogResult += CheckListDialog_DialogResult;
        }
        #endregion

        #region event handlers
        private void CheckListDialog_DialogResult(bool result)
        {
            DialogResult = result;
        }
        #endregion
    }
}
