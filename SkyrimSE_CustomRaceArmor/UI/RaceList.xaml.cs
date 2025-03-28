using Noggog;
using SSE.CRA.VM;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SSE.CRA.UI
{
    /// <summary>
    /// Interaction logic for RaceList.xaml
    /// </summary>
    public partial class RaceList : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<RaceViewModel>), typeof(RaceList), new FrameworkPropertyMetadata(null, ItemsSource_ValueChanged));
        public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(nameof(SelectedItems), typeof(IEnumerable<RaceViewModel>), typeof(RaceList), new FrameworkPropertyMetadata(Enumerable.Empty<RaceViewModel>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SelectedItems_ValueChanged));
        public static readonly DependencyProperty OnlyShowConfiguredProperty = DependencyProperty.Register(nameof(OnlyShowConfigured), typeof(bool), typeof(RaceList), new PropertyMetadata(false, OnlyShowConfigured_ValueChanged));
        public static readonly DependencyProperty OnlyShowSelectedProperty = DependencyProperty.Register(nameof(OnlyShowSelected), typeof(bool), typeof(RaceList), new PropertyMetadata(false, OnlyShowSelected_ValueChanged));
        public static readonly DependencyProperty ShowRefreshProperty = DependencyProperty.Register(nameof(ShowRefresh), typeof(bool), typeof(RaceList), new PropertyMetadata(false));
        public static readonly DependencyProperty RefreshCommandProperty = DependencyProperty.Register(nameof(RefreshCommand), typeof(ICommand), typeof(RaceList), new PropertyMetadata(null));
        public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(nameof(FilterText), typeof(string), typeof(RaceList), new PropertyMetadata("", FilterText_ValueChanged));

        #region fields
        private IEnumerable<ItemViewModel>? _allViewModels;
        private IEnumerable<ItemViewModel>? _viewModels;
        private bool _filterTextFocused;
        #endregion

        #region properties
        public IEnumerable<RaceViewModel>? ItemsSource
        {
            get => (IEnumerable<RaceViewModel>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }
        public IEnumerable<RaceViewModel> SelectedItems
        {
            get => (IEnumerable<RaceViewModel>)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }
        public bool OnlyShowConfigured
        {
            get => (bool)GetValue(OnlyShowConfiguredProperty);
            set => SetValue(OnlyShowConfiguredProperty, value);
        }
        public bool OnlyShowSelected
        {
            get => (bool)GetValue(OnlyShowSelectedProperty);
            set => SetValue(OnlyShowSelectedProperty, value);
        }
        public bool ShowRefresh
        {
            get => (bool)GetValue(ShowRefreshProperty);
            set => SetValue(ShowRefreshProperty, value);
        }
        public ICommand RefreshCommand
        {
            get => (ICommand)GetValue(RefreshCommandProperty);
            set => SetValue(RefreshCommandProperty, value);
        }
        public string FilterText
        {
            get => (string)GetValue(FilterTextProperty);
            set => SetValue(FilterTextProperty, value);
        }
        public bool ShowFilterTextOverlay => !_filterTextFocused && string.IsNullOrEmpty(FilterText);
        public IEnumerable<ItemViewModel>? ItemViewModels => _viewModels;
        public DelegateCommandBase ClearFilterTextCommand { get; private set; }
        #endregion

        #region events
        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion

        #region ctors
        public RaceList()
        {
            InitializeComponent();
            ClearFilterTextCommand = new DelegateCommand(ClearFilterText, CanClearFilterText);
        }
        #endregion

        #region methods (helping)
        private void UpdateEnabled()
        {
            ClearFilterTextCommand.RaiseCanExecuteChanged();
        }
        private void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private void UpdateViewModels(IEnumerable<RaceViewModel>? newModels)
        {
            if (_allViewModels is not null)
            {
                foreach (var item in _allViewModels)
                {
                    item.IsCheckedChangedByUser -= ItemViewModel_IsCheckedChangedByUser;
                    item.Model.PropertyChanged -= RaceViewModel_PropertyChanged;
                    item.Dispose();
                }
            }
            if (newModels is null)
            {
                _allViewModels = null;
                SelectedItems = [];
            }
            else
            {
                _allViewModels = newModels.Select(item => new ItemViewModel(item)).ToArray();
                foreach (var item in _allViewModels)
                {
                    item.IsCheckedChangedByUser += ItemViewModel_IsCheckedChangedByUser;
                    item.Model.PropertyChanged += RaceViewModel_PropertyChanged;
                    item.ChangeIsChecked(SelectedItems.Contains(item.Model));
                }
            }
            FilterViewModels();
        }
        private void FilterViewModels()
        {
            if(_allViewModels is not null)
            {
                _viewModels = _allViewModels.Where(vm => (!OnlyShowConfigured || vm.Model.Settings is not null) && (!OnlyShowSelected || vm.IsChecked) && (string.IsNullOrWhiteSpace(FilterText) || vm.DisplayName.Contains(FilterText.Trim(), StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                _viewModels = null;
            }
            RaisePropertyChanged(nameof(ItemViewModels));
        }
        private bool CanClearFilterText()
        {
            return !string.IsNullOrWhiteSpace(FilterText);
        }
        private void ClearFilterText()
        {
            FilterText = "";
        }
        #endregion

        #region event handlers
        private void ItemViewModel_IsCheckedChangedByUser(object? sender, EventArgs e)
        {
            if (_allViewModels is not null) SelectedItems = [.. _allViewModels.Where(vm => vm.IsChecked).Select(vm => vm.Model)];
        }
        private void RaceViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(RaceViewModel.Settings))
            {
                FilterViewModels();
            }
        }
        private void FilterText_GotFocus(object sender, RoutedEventArgs e)
        {
            _filterTextFocused = true;
            RaisePropertyChanged(nameof(ShowFilterTextOverlay));
        }
        private void FilterText_LostFocus(object sender, RoutedEventArgs e)
        {
            _filterTextFocused = false;
            RaisePropertyChanged(nameof(ShowFilterTextOverlay));
        }
        private static void ItemsSource_ValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var list = (RaceList)sender;
            list.UpdateViewModels((IEnumerable<RaceViewModel>?)e.NewValue);
        }
        private static void SelectedItems_ValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var list = (RaceList)sender;
            if (list._allViewModels is null) return;
            var selected = (IEnumerable<RaceViewModel>)e.NewValue;
            foreach (var item in list._allViewModels)
            {
                item.ChangeIsChecked(selected.Contains(item.Model));
            }
            list.FilterViewModels();
        }
        private static void OnlyShowConfigured_ValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var list = (RaceList)sender;
            list.FilterViewModels();
        }
        private static void OnlyShowSelected_ValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var list = (RaceList)sender;
            list.FilterViewModels();
        }
        private static void FilterText_ValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var list = (RaceList)sender;
            list.RaisePropertyChanged(nameof(ShowFilterTextOverlay));
            list.FilterViewModels();
            list.UpdateEnabled();
        }
        #endregion

        public class ItemViewModel : BaseViewModel, IDisposable, IEditableObject
        {
            #region fields
            public readonly RaceViewModel Model;
            private bool _inEditMode = false;
            private bool _isChecked = false;
            private bool _cancelIsChecked;
            #endregion

            #region properties
            public string DisplayName => (Model.Settings is null ? "" : "(configured) ") + Model.Name;
            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked != value)
                    {
                        _isChecked = value;
                        RaisePropertyChanged();
                        IsCheckedChangedByUser?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            #endregion

            #region events
            public event EventHandler? IsCheckedChangedByUser;
            #endregion

            #region ctors
            public ItemViewModel(RaceViewModel model)
            {
                Model = model;
                Model.PropertyChanged += Model_PropertyChanged;
            }
            #endregion

            #region methods
            public void ChangeIsChecked(bool newVal)
            {
                if (_isChecked != newVal)
                {
                    _isChecked = newVal;
                    RaisePropertyChanged(nameof(IsChecked));
                }
            }
            public void BeginEdit()
            {
                if (!_inEditMode)
                {
                    _inEditMode = true;
                    _cancelIsChecked = _isChecked;
                }
            }
            public void CancelEdit()
            {
                if (_inEditMode)
                {
                    _inEditMode = false;
                    _isChecked = _cancelIsChecked;
                }
            }
            public void EndEdit()
            {
                _inEditMode = false;
            }
            public void Dispose()
            {
                Model.PropertyChanged -= Model_PropertyChanged;
            }
            #endregion

            #region event handlers
            private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(Model.Settings))
                {
                    RaisePropertyChanged(nameof(DisplayName));
                }
            }
            #endregion
        }
    }
}
