namespace SSE.CRA.VM
{
    internal class CheckListDialogViewModel : BaseViewModel
    {
        #region fields
        private string _confirmText = "OK";
        private System.Collections.IEnumerable? _itemsSource = null;
        private IEnumerable<CheckItemViewModel> _items = [];
        private Func<object, string>? _itemNameGetter = null;
        private Func<object, bool>? _itemPreselecter = null;
        #endregion

        #region properties
        public string ConfirmText
        {
            get => _confirmText;
            set
            {
                if (_confirmText != value)
                {
                    _confirmText = value;
                    RaisePropertyChanged();
                }
            }
        }
        public System.Collections.IEnumerable? ItemsSource
        {
            get => _itemsSource;
            set
            {
                if (!ReferenceEquals(_itemsSource, value))
                {
                    _itemsSource = value;
                    _items = value?.Cast<object>().Select(m => new CheckItemViewModel(m) { ItemNameGetter = _itemNameGetter }).ToArray() ?? [];
                    RaisePropertyChanged(nameof(Items));
                }
            }
        }
        public Func<object, string>? ItemNameGetter
        {
            get => _itemNameGetter;
            set
            {
                _itemNameGetter = value;
                foreach (var item in _items)
                {
                    item.ItemNameGetter = value;
                }
            }
        }
        public Func<object, bool>? ItemPreselecter
        {
            get => _itemPreselecter;
            set
            {
                _itemPreselecter = value;
                if (value is not null)
                {
                    foreach (var item in _items)
                    {
                        item.Checked = value.Invoke(item);
                    }
                }
            }
        }
        public IEnumerable<CheckItemViewModel> Items => _items;
        public DelegateCommandBase ConfirmCommand { get; private set; }
        public DelegateCommandBase CancelCommand { get; private set; }
        public IEnumerable<object> SelectedItems { get; private set; } = [];
        #endregion

        #region events
        public event DialogResultEventHandler? DialogResult;
        #endregion

        #region ctors
        public CheckListDialogViewModel()
        {
            ConfirmCommand = new DelegateCommand(Confirm);
            CancelCommand = new DelegateCommand(Cancel);
        }
        #endregion

        #region methods (helping)
        private void Confirm()
        {
            SelectedItems = _items.Where(vm => vm.Checked).Select(vm => vm.Model).ToArray();
            DialogResult?.Invoke(true);
        }
        private void Cancel()
        {
            DialogResult?.Invoke(false);
        }
        #endregion

        public class CheckItemViewModel : BaseViewModel
        {
            #region fields
            public readonly object Model;
            private Func<object, string>? _itemNameGetter;
            private bool _checked;
            #endregion

            #region properties
            public string Name
            {
                get
                {
                    return _itemNameGetter?.Invoke(Model) ?? Model.ToString() ?? "...what should I show here?!";
                }
            }
            public bool Checked
            {
                get => _checked;
                set
                {
                    if (_checked != value)
                    {
                        _checked = value;
                        RaisePropertyChanged();
                    }
                }
            }
            public Func<object, string>? ItemNameGetter
            {
                get => _itemNameGetter;
                set
                {
                    if (_itemNameGetter != value)
                    {
                        _itemNameGetter = value;
                        RaisePropertyChanged(nameof(Name));
                    }
                }
            }
            #endregion

            #region ctors
            public CheckItemViewModel(object model)
            {
                Model = model;
            }
            #endregion
        }
    }
}
