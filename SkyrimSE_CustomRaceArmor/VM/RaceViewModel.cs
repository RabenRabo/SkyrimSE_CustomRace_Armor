using SSE.CRA.AL;
using System.Collections.ObjectModel;

namespace SSE.CRA.VM
{
    public class RaceViewModel : BaseViewModel, IEquatable<RaceViewModel>
    {
        #region fields
        public readonly string EditorID;
        private RaceSettings? _settings;
        private readonly ObservableCollection<ReplacerRegexViewModel> _replacerRegexes = [];
        private ReplacerRegexViewModel? _selectedReplacerRegex;
        private IEnumerable<RaceViewModel> _additionalRaces = [];
        private IEnumerable<RaceViewModel> _compatibleArmorRaces;
        #endregion

        #region properties
        public RaceSettings? Settings
        {
            get => _settings;
            private set
            {
                if (_settings != value)
                {
                    _settings = value;
                    RaisePropertyChanged();
                }
            }
        }
        public string Name
        {
            get => EditorID;
        }
        public bool HasCustomHeadAA
        {
            get => Settings?.CustomHead ?? false;
            set
            {
                if (HasCustomHeadAA != value)
                {
                    Settings ??= CreateSettings();
                    Settings.CustomHead = value;
                    RaisePropertyChanged();
                }
            }
        }
        public bool HasCustomBodyAA
        {
            get => Settings?.CustomBody ?? true;
            set
            {
                if (HasCustomBodyAA != value)
                {
                    Settings ??= CreateSettings();
                    Settings.CustomBody = value;
                    RaisePropertyChanged();
                }
            }
        }
        public bool HasCustomHandsAA
        {
            get => Settings?.CustomHands ?? false;
            set
            {
                if (HasCustomHandsAA != value)
                {
                    Settings ??= CreateSettings();
                    Settings.CustomHands = value;
                    RaisePropertyChanged();
                }
            }
        }
        public bool HasCustomFeetAA
        {
            get => Settings?.CustomFeet ?? true;
            set
            {
                if (HasCustomFeetAA != value)
                {
                    Settings ??= CreateSettings();
                    Settings.CustomFeet = value;
                    RaisePropertyChanged();
                }
            }
        }
        public bool ProcessMale
        {
            get => Settings?.ProcessMale ?? true;
            set
            {
                if (ProcessMale != value)
                {
                    Settings ??= CreateSettings();
                    Settings.ProcessMale = value;
                    RaisePropertyChanged();
                }
            }
        }
        public bool ProcessFemale
        {
            get => Settings?.ProcessFemale ?? true;
            set
            {
                if (ProcessFemale != value)
                {
                    Settings ??= CreateSettings();
                    Settings.ProcessFemale = value;
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
        public IEnumerable<RaceViewModel> AdditionalRaces
        {
            get => _additionalRaces;
            set
            {
                Settings ??= CreateSettings();
                _additionalRaces = value;
                Settings.AdditionalRaces = [.. value.Select(ar => ar.EditorID)];
                RaisePropertyChanged();
            }
        }
        public IEnumerable<RaceViewModel> CompatibleArmorRaces
        {
            get => _compatibleArmorRaces;
            set
            {
                Settings ??= CreateSettings();
                _compatibleArmorRaces = value;
                Settings.CompatibleArmorRaces = [.. value.Select(ar => ar.EditorID)];
                RaisePropertyChanged();
            }
        }
        public DelegateCommandBase MoveReplacerRegexUpCommand { get; private set; }
        public DelegateCommandBase MoveReplacerRegexDownCommand { get; private set; }
        #endregion

        #region events
        public event EventHandler? UpdateEnabledRequested;
        #endregion

        #region ctors
        public RaceViewModel(string editorID, IEnumerable<RaceViewModel> vanilla)
        {
            EditorID = editorID;
            MoveReplacerRegexUpCommand = new DelegateCommand(MoveReplacerRegexUp, CanMoveReplacerRegexUp);
            MoveReplacerRegexDownCommand = new DelegateCommand(MoveReplacerRegexDown, CanMoveReplacerRegexDown);
            var rr = new ReplacerRegexViewModel() { SearchRegex = "(.+)", ReplaceString = "Patched\\$1" };
            _replacerRegexes.Add(rr);
            _replacerRegexes.CollectionChanged += ReplacerRegexes_CollectionChanged;
            var defRace = vanilla.FirstOrDefault(r => r.EditorID == "DefaultRace");
            _compatibleArmorRaces = defRace is null ? [] : [defRace];
        }
        #endregion

        #region methods
        public bool TryLoadRaceSettings(IEnumerable<IRaceSettingsAL> raceSettingsALs, IEnumerable<RaceViewModel> nonVanillaRaces, IEnumerable<RaceViewModel> vanillaRaces)
        {
            IRaceSettingsAL? raceSettingsAL = null;
            foreach (var al in raceSettingsALs)
            {
                if (al.Exists(EditorID))
                {
                    raceSettingsAL = al;
                    break;
                }
            }
            if (raceSettingsAL is null) return false;
            Settings = raceSettingsAL.Load(EditorID);
            _replacerRegexes.CollectionChanged -= ReplacerRegexes_CollectionChanged;
            _replacerRegexes.Clear();
            foreach (var rr in Settings.RegexReplacers)
            {
                var rrvm = new ReplacerRegexViewModel() { SearchRegex = rr.Key, ReplaceString = rr.Value };
                rrvm.PropertyChanged += ReplacerRegex_PropertyChanged;
                _replacerRegexes.Add(rrvm);
            }
            _replacerRegexes.CollectionChanged += ReplacerRegexes_CollectionChanged;
            ReplacerRegexes_CollectionChanged(_replacerRegexes, new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
            List<RaceViewModel> addRaces = [];
            foreach (var addRace in Settings.AdditionalRaces)
            {
                RaceViewModel? vm = nonVanillaRaces.FirstOrDefault(r => r.EditorID == addRace);
                if (vm is not null) addRaces.Add(vm);
            }
            _additionalRaces = addRaces;
            RaisePropertyChanged(nameof(AdditionalRaces));
            List<RaceViewModel> compArmorRaces = [];
            foreach (var compArmorRace in Settings.CompatibleArmorRaces)
            {
                RaceViewModel? vm = vanillaRaces.FirstOrDefault(r => r.EditorID == compArmorRace);
                if (vm is not null) compArmorRaces.Add(vm);
            }
            _compatibleArmorRaces = compArmorRaces;
            RaisePropertyChanged(nameof(CompatibleArmorRaces));
            return true;
        }
        public void SaveRaceSettings(IRaceSettingsAL raceSettingsAL)
        {
            Settings ??= CreateSettings();
            raceSettingsAL.Save(EditorID, Settings);
        }
        public override string ToString()
        {
            return Name;
        }
        public bool Equals(RaceViewModel? other)
        {
            return other is not null && EditorID == other.EditorID;
        }
        public override bool Equals(object? obj)
        {
            return obj is RaceViewModel other && EditorID == other.EditorID;
        }
        public override int GetHashCode()
        {
            return EditorID.GetHashCode();
        }
        #endregion

        #region methods (helping)
        private RaceSettings CreateSettings()
        {
            return new RaceSettings()
            {
                CustomHead = HasCustomHeadAA,
                CustomBody = HasCustomBodyAA,
                CustomHands = HasCustomHandsAA,
                CustomFeet = HasCustomFeetAA,
                ProcessMale = ProcessMale,
                ProcessFemale = ProcessFemale,
                RegexReplacers = [.. ReplacerRegexes.Select(rr => new KeyValuePair<string, string>(rr.SearchRegex, rr.ReplaceString))],
                AdditionalRaces = [.. AdditionalRaces.Select(ar => ar.EditorID)],
                CompatibleArmorRaces = [.. CompatibleArmorRaces.Select(ar => ar.EditorID)]
            };
        }
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
            Settings ??= CreateSettings();
            Settings.RegexReplacers = [.. ReplacerRegexes.Select(rr => new KeyValuePair<string, string>(rr.SearchRegex, rr.ReplaceString))];
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
