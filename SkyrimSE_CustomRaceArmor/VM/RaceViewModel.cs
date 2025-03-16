using SSE.CRA.AL;
using System.Collections.ObjectModel;
using static SSE.CRA.BL.Modding;

namespace SSE.CRA.VM
{
    internal class RaceViewModel : BaseViewModel
    {
        #region fields
        public readonly RaceEditorIDPair Model;
        private bool _toBeProcessed;
        private bool _hasCustomHeadAA = false;
        private bool _hasCustomBodyAA = true;
        private bool _hasCustomHandsAA = false;
        private bool _hasCustomFeetAA = true;
        private bool _processMale = false;
        private bool _processFemale = true;
        private readonly ObservableCollection<ReplacerRegexViewModel> _replacerRegexes = [];
        private ReplacerRegexViewModel? _selectedReplacerRegex;
        #endregion

        #region properties
        public bool ToBeProcessed
        {
            get => _toBeProcessed;
            set
            {
                if (_toBeProcessed != value)
                {
                    _toBeProcessed = value;
                    RaisePropertyChanged();
                }
            }
        }
        public string Name
        {
            get => Model.Main;
        }
        public bool HasCustomHeadAA
        {
            get => _hasCustomHeadAA;
            set
            {
                if (_hasCustomHeadAA != value)
                {
                    _hasCustomHeadAA = value;
                    RaisePropertyChanged();
                }
            }
        }
        public bool HasCustomBodyAA
        {
            get => _hasCustomBodyAA;
            set
            {
                if (_hasCustomBodyAA != value)
                {
                    _hasCustomBodyAA = value;
                    RaisePropertyChanged();
                }
            }
        }
        public bool HasCustomHandsAA
        {
            get => _hasCustomHandsAA;
            set
            {
                if (_hasCustomHandsAA != value)
                {
                    _hasCustomHandsAA = value;
                    RaisePropertyChanged();
                }
            }
        }
        public bool HasCustomFeetAA
        {
            get => _hasCustomFeetAA;
            set
            {
                if (_hasCustomFeetAA != value)
                {
                    _hasCustomFeetAA = value;
                    RaisePropertyChanged();
                }
            }
        }
        public bool ProcessMale
        {
            get => _processMale;
            set
            {
                if (_processMale != value)
                {
                    _processMale = value;
                    RaisePropertyChanged();
                }
            }
        }
        public bool ProcessFemale
        {
            get => _processFemale;
            set
            {
                if (_processFemale != value)
                {
                    _processFemale = value;
                    RaisePropertyChanged();
                }
            }
        }
        public ObservableCollection<ReplacerRegexViewModel> ReplacerRegexes => _replacerRegexes;
        public ReplacerRegexViewModel? SelectedReplacerRegex
        {
            get => _selectedReplacerRegex;
            set
            {
                if (_selectedReplacerRegex != value)
                {
                    _selectedReplacerRegex = value;
                    RaisePropertyChanged();
                }
            }
        }
        public DelegateCommandBase MoveReplacerRegexUpCommand { get; private set; }
        public DelegateCommandBase MoveReplacerRegexDownCommand { get; private set; }
        #endregion

        #region events
        public event EventHandler? UpdateEnabledRequested;
        #endregion

        #region ctors
        public RaceViewModel(RaceEditorIDPair model)
        {
            Model = model;
            MoveReplacerRegexUpCommand = new DelegateCommand(MoveReplacerRegexUp, CanMoveReplacerRegexUp);
            MoveReplacerRegexDownCommand = new DelegateCommand(MoveReplacerRegexDown, CanMoveReplacerRegexDown);
            var rr = new ReplacerRegexViewModel() { SearchRegex = "(.+)", ReplaceString = "Patched\\$1" };
            _replacerRegexes.Add(rr);
            _replacerRegexes.CollectionChanged += ReplacerRegexes_CollectionChanged;
        }
        #endregion

        #region methods
        public bool TryLoadingRaceSettings()
        {
            IRaceSettingsAL? raceSettingsAL = null;
            foreach (var al in MainViewModel.RaceSettingsALs)
            {
                if (al.Exists(Model.Main))
                {
                    raceSettingsAL = al;
                    break;
                }
            }
            if (raceSettingsAL is null) return false;
            RaceSettings settings = raceSettingsAL.Load(Model.Main);
            HasCustomHeadAA = settings.CustomHead;
            HasCustomBodyAA = settings.CustomBody;
            HasCustomHandsAA = settings.CustomHands;
            HasCustomFeetAA = settings.CustomFeet;
            ProcessMale = settings.ProcessMale;
            ProcessFemale = settings.ProcessFemale;
            _replacerRegexes.Clear();
            foreach (var rr in settings.RegexReplacers)
            {
                _replacerRegexes.Add(new ReplacerRegexViewModel() { SearchRegex = rr.Key, ReplaceString = rr.Value });
            }
            return true;
        }
        public override string ToString()
        {
            return Name;
        }
        #endregion

        #region methods (helping)
        private void UpdateEnabled()
        {
            MoveReplacerRegexDownCommand.RaiseCanExecuteChanged();
            MoveReplacerRegexUpCommand.RaiseCanExecuteChanged();
            UpdateEnabledRequested?.Invoke(this, EventArgs.Empty);
        }
        private bool CanMoveReplacerRegexUp()
        {
            return SelectedReplacerRegex is not null && SelectedReplacerRegex.Index > 0;
        }
        private void MoveReplacerRegexUp()
        {
            _replacerRegexes.Move(SelectedReplacerRegex!.Index, SelectedReplacerRegex!.Index - 1);
            ReindexRegexReplacers();
            UpdateEnabled();
        }
        private bool CanMoveReplacerRegexDown()
        {
            return SelectedReplacerRegex is not null && SelectedReplacerRegex.Index < _replacerRegexes.Count - 1;
        }
        private void MoveReplacerRegexDown()
        {
            _replacerRegexes.Move(SelectedReplacerRegex!.Index, SelectedReplacerRegex!.Index + 1);
            ReindexRegexReplacers();
            UpdateEnabled();
        }
        #endregion

        #region event handlers
        private void ReplacerRegexes_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    foreach (var vm in e.NewItems!.Cast<ReplacerRegexViewModel>())
                    {
                        vm.PropertyChanged += ReplacerRegex_PropertyChanged;
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    foreach (var vm in e.OldItems!.Cast<ReplacerRegexViewModel>())
                    {
                        vm.PropertyChanged -= ReplacerRegex_PropertyChanged;
                    }
                    break;
            }
            ReindexRegexReplacers();
        }
        public void ReindexRegexReplacers()
        {
            for (int i = 0; i < _replacerRegexes.Count; i++)
            {
                _replacerRegexes[i].Index = i;
            }
        }
        private void ReplacerRegex_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateEnabled();
        }
        #endregion
    }
}
