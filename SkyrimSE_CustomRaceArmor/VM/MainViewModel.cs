using Microsoft.Win32;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using SSE.CRA.AL;
using SSE.CRA.BL;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SSE.CRA.VM
{
    internal class MainViewModel : BaseViewModel
    {
        public static readonly IEnumerable<IRaceSettingsAL> RaceSettingsALs = [new RaceSettingsJsonAL() { Directory = MainViewModel.RaceSettingsDefaultDirectory }];
        public const string RaceSettingsDefaultDirectory = "RaceSettings";
        public const string GeneralSettingsDefaultFile = "GeneralSettings.json";

        #region fields
        private readonly string _gameDataPath;
        private bool _running = false;
        private bool _showRaceConfiguration = false;
        private readonly IEnumerable<RaceViewModel> _races;
        private RaceViewModel? _selectedRace;

        private string[] _nonWearableArmorRegexes = [@"^(?:dlc\d+\\)?actors\\", @"^(?:dlc\d+\\)?effects\\"];
        private string _outputName = "CustomRacesArmor";
        private StringBuilder _consoleText = new StringBuilder();
        private ProgressInfoTypes _selectedConsoleLevel = ProgressInfoTypes.Info;
        private readonly GeneralSettings _generalSettings;
        #endregion

        #region properties
        public DelegateCommandBase SaveRaceSettingsCommand { get; private set; }
        public DelegateCommandBase LoadRaceSettingsCommand { get; private set; }
        public DelegateCommandBase PatchCommand { get; private set; }
        public DelegateCommandBase SaveConsoleToFileCommand { get; private set; }
        public bool Running => _running;
        public bool ShowRaceConfiguration
        {
            get => _showRaceConfiguration;
            set
            {
                if (_showRaceConfiguration != value)
                {
                    _showRaceConfiguration = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(RaceConfigurationToggleButtonContent));
                }
            }
        }
        public string RaceConfigurationToggleButtonContent => _showRaceConfiguration ? "v" : ">";
        public IEnumerable<RaceViewModel> Races => _races;
        public RaceViewModel? SelectedRace
        {
            get => _selectedRace;
            set
            {
                if (!ReferenceEquals(_selectedRace, value))
                {
                    _selectedRace = value;
                    UpdateEnabled();
                    RaisePropertyChanged();
                }
            }
        }
        public string OutputName
        {
            get => _outputName;
            set
            {
                if (_outputName != value)
                {
                    _outputName = value;
                    UpdateEnabled();
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(OutputNameValid));
                }
            }
        }
        public bool OutputNameValid => !string.IsNullOrEmpty(_outputName);
        public IEnumerable<ProgressInfoTypes> ConsoleLevelItems => Enum.GetValues(typeof(ProgressInfoTypes)).Cast<ProgressInfoTypes>();
        public ProgressInfoTypes SelectedConsoleLevel
        {
            get => _selectedConsoleLevel;
            set
            {
                if (_selectedConsoleLevel != value)
                {
                    _selectedConsoleLevel = value;
                    RaisePropertyChanged();
                }
            }
        }
        #endregion

        #region events
        public event ConsoleTextEventHandler? ConsoleTextChanged;
        public event EventHandler? ConsoleTextCleared;
        #endregion

        #region ctors
        public MainViewModel()
        {
            SaveRaceSettingsCommand = new DelegateCommand(SaveRaceSettings, CanSaveRaceSettings);
            LoadRaceSettingsCommand = new DelegateCommand(LoadRaceSettings, CanLoadRaceSettings);
            PatchCommand = new AsyncDelegateCommand(Patch, CanPatch);
            SaveConsoleToFileCommand = new DelegateCommand(SaveConsoleToFile);
            var al = new GeneralSettingsJsonAL(GeneralSettingsDefaultFile);
            _generalSettings = al.Load();
            using (var modding = new Modding())
            {
                _gameDataPath = modding.GetGameDataPath();
                _races = modding.GetRaces().Select(pair => new RaceViewModel(pair.Key.EditorID!, pair.Value.EditorID!)).ToArray();
            }
            foreach (var race in _races)
            {
                race.TryLoadingRaceSettings();
                race.PropertyChanged += Race_PropertyChanged;
                race.UpdateEnabledRequested += Race_UpdateEnabledRequested;
            }
        }
        #endregion

        #region methods (helping)
        private void UpdateEnabled()
        {
            PatchCommand.RaiseCanExecuteChanged();
            SaveRaceSettingsCommand.RaiseCanExecuteChanged();
            LoadRaceSettingsCommand.RaiseCanExecuteChanged();
        }
        private bool CanSaveRaceSettings()
        {
            return SelectedRace is not null;
        }
        private void SaveRaceSettings()
        {
            if (SelectedRace is null) return;
            string defDir = Path.Combine(Directory.GetCurrentDirectory(), RaceSettingsDefaultDirectory);
            if (!Directory.Exists(defDir))
            {
                Directory.CreateDirectory(defDir);
            }
            var dlg = new SaveFileDialog()
            {
                InitialDirectory = defDir,
                Filter = CreateRaceSettingsDialogFilter(),
                FileName = RaceSettingsALs.FirstOrDefault()?.ConstructFilename(SelectedRace!.EditorID) ?? "",
                AddExtension = true,
                OverwritePrompt = true,
                Title = "save race settings to file"
            };
            if (dlg.ShowDialog() == true)
            {
                string? ext = Path.GetExtension(dlg.FileName);
                if (ext is not null) ext = ext[1..];
                IRaceSettingsAL? al = RaceSettingsALs.FirstOrDefault(a => string.Equals(a.FileExtension, ext));
                if (al is null)
                {
                    ConsoleTextChanged?.Invoke($"ERROR: unknown file extension {ext}", true);
                    return;
                }
                var settings = new RaceSettings()
                {
                    CustomHead = SelectedRace.HasCustomHeadAA,
                    CustomBody = SelectedRace.HasCustomBodyAA,
                    CustomHands = SelectedRace.HasCustomHandsAA,
                    CustomFeet = SelectedRace.HasCustomFeetAA,
                    ProcessMale = SelectedRace.ProcessMale,
                    ProcessFemale = SelectedRace.ProcessFemale,
                    RegexReplacers = SelectedRace.ReplacerRegexes.Select(rr => new KeyValuePair<string, string>(rr.SearchRegex, rr.ReplaceString)).ToArray()
                };
                try
                {
                    al.Save(SelectedRace.EditorID, settings);
                }
                catch (Exception ex)
                {
                    ConsoleTextChanged?.Invoke($"ERROR: while saving race settings: {ex.Message}", true);
                }
            }
        }
        private bool CanLoadRaceSettings()
        {
            return SelectedRace is not null;
        }
        private void LoadRaceSettings()
        {
            throw new NotImplementedException();
        }
        private string CreateRaceSettingsDialogFilter()
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (var al in RaceSettingsALs)
            {
                if (first) first = false;
                else sb.Append('|');
                sb.Append(al.FileFilter).Append(" (*.").Append(al.FileExtension).Append("|*.").Append(al.FileExtension);
            }
            if (!first) sb.Append('|');
            sb.Append("all files (*.*)|*.*");
            return sb.ToString();
        }
        private bool CanPatch()
        {
            return OutputNameValid && _races.Any(r => r.ToBeProcessed);
        }
        private async Task Patch()
        {
            IProgress<ProgressInfo> progress = new Progress<ProgressInfo>();
            ((Progress<ProgressInfo>)progress).ProgressChanged += Progress_ProgressChanged;
            _consoleText.Clear();
            ConsoleTextCleared?.Invoke(this, EventArgs.Empty);
            _running = true;
            RaisePropertyChanged(nameof(Running));
            await Task.Run(() =>
            {
                Modding? modding = null;
                try
                {
                    var modKey = new ModKey(OutputName, ModType.Plugin);
                    if (Modding.ExistsMod(_gameDataPath, modKey))
                    {
                        progress.Report(new ProgressInfo(ProgressInfoTypes.Info, "deleting old modfile"));
                        Modding.DeleteMod(_gameDataPath, modKey);
                    }
                    modding = new Modding();
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "creating mod object"));
                    SkyrimMod mod = modding.CreateMod(modKey);
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Debug, "created mod object"));
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "removing ArmorRace from race"));
                    var races = _races.Where(r => r.ToBeProcessed).Select(r => new KeyValuePair<RaceViewModel, Modding.RacePair>(r, modding.RemoveArmorRace(mod, r.EditorID, r.VampEditorID))).ToArray();
                    var racesInfo = races.Select(pair =>
                        new Modding.ArmorRaceProcessingInfo(
                            pair.Value,
                            pair.Key.HasCustomHeadAA,
                            pair.Key.HasCustomBodyAA,
                            pair.Key.HasCustomHandsAA,
                            pair.Key.HasCustomFeetAA,
                            pair.Key.ProcessMale,
                            pair.Key.ProcessFemale,
                            pair.Key.ReplacerRegexes.Select(vm => new KeyValuePair<Regex, string>(new Regex(vm.SearchRegex, RegexOptions.IgnoreCase), vm.ReplaceString)).ToArray())).ToArray();
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Debug, "removed ArmorRace from race"));
                    var processingInfo = new Modding.ArmorProcessingInfo(
                        mod,
                        racesInfo,
                        _generalSettings,
                        _nonWearableArmorRegexes.Select(nar => new Regex(nar, RegexOptions.IgnoreCase)).ToArray()
                    );
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "updating race skin armor"));
                    int countSkinAAs = modding.SetArmorRaceOfSkin(processingInfo);
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Debug, $"updated race skin armor ({countSkinAAs} ArmorAddons updated)"));
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "processing armor"));
                    var result = modding.ProcessArmor(processingInfo, progress, null);
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Info, $"processed {result.ArmorCount} armor (overrode {result.OverwriteAAs} ArmorAddons, created {result.NewAAs} new ones, missing {result.MissingPaths} models)"));
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "writing mod to disk"));
                    modding.WriteMod(processingInfo);
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Info, $"created {processingInfo.Target.ModKey.FileName}"));
                }
                catch (Exception ex)
                {
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Error, $"patch process aborted: {ex.Message}"));
                }
                finally
                {
                    modding?.Dispose();
                }
            });
            _running = false;
            RaisePropertyChanged(nameof(Running));
        }
        private void SaveConsoleToFile()
        {
            var saveFileDialog = new SaveFileDialog()
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                FileName = "ArmorPatch.log",
                Title = "save console output to file",
                Filter = "log file (*.log)|*.log|all files (*.*)|*.*"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(saveFileDialog.FileName))
                {
                    writer.Write(_consoleText.ToString());
                }
            }
        }
        #endregion

        #region event handlers
        private void Progress_ProgressChanged(object? sender, ProgressInfo e)
        {
            App.Current.MainWindow?.Dispatcher.BeginInvoke(() =>
            {
                if (e.Type >= SelectedConsoleLevel)
                {
                    _consoleText.Append(e.Type.ToString().ToUpper()).Append(": ").AppendLine(e.Message);
                    ConsoleTextChanged?.Invoke(e.Type.ToString().ToUpper(), false);
                    ConsoleTextChanged?.Invoke(": ", false);
                    ConsoleTextChanged?.Invoke(e.Message, true);
                }
            });
        }
        private void Race_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RaceViewModel.ToBeProcessed))
            {
                UpdateEnabled();
            }
        }
        private void Race_UpdateEnabledRequested(object? sender, EventArgs e)
        {
            UpdateEnabled();
        }
        #endregion
    }

    public delegate void ConsoleTextEventHandler(string msg, bool newline);
}
