using Microsoft.Win32;
using Mutagen.Bethesda.Skyrim;
using SSE.CRA.AL;
using SSE.CRA.BL;
using SSE.CRA.UI;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace SSE.CRA.VM
{
    internal class MainViewModel : BaseViewModel
    {
        private const string DefaultOutputName = "CustomRacesArmor";
        private const bool DefaultFlagESL = true;
        private const int DefaultMaxPluginMasters = 200;
        private const int DefaultMaxNewRecords = 2000;

        public static readonly IEnumerable<IRaceSettingsAL> RaceSettingsALs = [new RaceSettingsJsonAL() { Directory = MainViewModel.RaceSettingsDefaultDirectory }];
        public const string RaceSettingsDefaultDirectory = "RaceSettings";
        public const string GeneralSettingsDefaultFile = "GeneralSettings.json";
        public static readonly SkyrimRelease[] SkyrimVersions = [SkyrimRelease.SkyrimSE, SkyrimRelease.SkyrimSEGog, SkyrimRelease.SkyrimVR];

        #region fields
        private bool _running = false;
        private readonly IUserSettingsAL _userSettingsAL = new UserSettingsJsonAL("Settings.user");
        private readonly Dictionary<SkyrimRelease, VersionUserSettings> _userSettings = [];
        private string _gameDataPath = "";
        private string? _customGameDataPath;
        private SkyrimRelease _selectedSkyrimVersion = SkyrimRelease.SkyrimSE;
        private IEnumerable<RaceViewModel> _racesToBeProcessed = [];
        private bool _showRaceConfiguration = false;
        private RaceViewModel? _selectedRace;
        private string[] _nonWearableArmorRegexes = [@"^(?:dlc\d+\\)?actors\\", @"^(?:dlc\d+\\)?effects\\"];
        private string _outputName = DefaultOutputName;
        private bool _flagESL = DefaultFlagESL;
        private int _maxNewRecordsInt = DefaultMaxNewRecords;
        private string _maxNewRecords = DefaultMaxNewRecords.ToString();
        private int _maxPluginMastersInt = DefaultMaxPluginMasters;
        private string _maxPluginMasters = DefaultMaxPluginMasters.ToString();
        private readonly StringBuilder _consoleText = new();
        private ProgressInfoTypes _selectedConsoleLevel = UserSettings.DefaultLogLevel;
        private readonly GeneralSettings _generalSettings;
        #endregion

        #region properties
        public DelegateCommandBase RefreshRacesCommand { get; private set; }
        public DelegateCommandBase SaveRaceSettingsCommand { get; private set; }
        public DelegateCommandBase LoadRaceSettingsCommand { get; private set; }
        public DelegateCommandBase PatchCommand { get; private set; }
        public DelegateCommandBase SaveConsoleToFileCommand { get; private set; }
        public DelegateCommandBase AboutCommand { get; private set; }
        public DelegateCommandBase SelectGameDataPathCommand { get; private set; }
        public DelegateCommandBase ResetGameDataPathCommand { get; private set; }
        public bool Running => _running;
        public IEnumerable<SkyrimRelease> SkyrimVersionItems => SkyrimVersions;
        public SkyrimRelease SelectedSkyrimVersion
        {
            get => _selectedSkyrimVersion;
            set
            {
                if (_selectedSkyrimVersion != value)
                {
                    ClearConsole();
                    RememberUserSettings();
                    _selectedSkyrimVersion = value;
                    RaisePropertyChanged();
                    ReadDefaultGameDataPath();
                    ApplyUserSettings();
                }
            }
        }
        public string GameDataPath => _customGameDataPath ?? _gameDataPath;
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
        public IEnumerable<RaceViewModel> Races { get; private set; } = [];
        public IEnumerable<RaceViewModel> RacesToBeProcessed
        {
            get => _racesToBeProcessed;
            set
            {
                _racesToBeProcessed = value;
                RaisePropertyChanged(nameof(RacesToBeProcessed));
                UpdateEnabled();
            }
        }
        public RaceViewModel? SelectedRace
        {
            get => _selectedRace;
            set
            {
                if (!ReferenceEquals(_selectedRace, value))
                {
                    _selectedRace = value;
                    if(value is null)
                    {
                        AdditionalRaces = [];
                    }
                    else
                    {
                        AdditionalRaces = Races.Where(r => !ReferenceEquals(r, value)).ToArray();
                    }
                    UpdateEnabled();
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(AdditionalRaces));
                }
            }
        }
        public IEnumerable<RaceViewModel> AdditionalRaces { get; private set; } = [];
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
        public bool FlagESL
        {
            get => _flagESL;
            set
            {
                if (_flagESL != value)
                {
                    _flagESL = value;
                    RaisePropertyChanged();
                }
            }
        }
        public string MaxNewRecords
        {
            get => _maxNewRecords;
            set
            {
                if (_maxNewRecords != value)
                {
                    _maxNewRecords = value;
                    if (int.TryParse(value, out int tmp))
                    {
                        _maxNewRecordsInt = tmp;
                    }
                    else
                    {
                        _maxNewRecordsInt = -1;
                    }
                    UpdateEnabled();
                    RaisePropertyChanged(nameof(MaxNewRecordsValid));
                    RaisePropertyChanged();
                }
            }
        }
        public bool MaxNewRecordsValid => _maxNewRecordsInt > 0 && _maxNewRecordsInt <= 2048;
        public string MaxPluginMasters
        {
            get => _maxPluginMasters;
            set
            {
                if (_maxPluginMasters != value)
                {
                    _maxPluginMasters = value;
                    if (int.TryParse(value, out int tmp))
                    {
                        _maxPluginMastersInt = tmp;
                    }
                    else
                    {
                        _maxPluginMastersInt = -1;
                    }
                    UpdateEnabled();
                    RaisePropertyChanged(nameof(MaxPluginMastersValid));
                    RaisePropertyChanged();
                }
            }
        }
        public bool MaxPluginMastersValid => _maxPluginMastersInt > 0 && _maxPluginMastersInt <= 254;
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
            SelectGameDataPathCommand = new DelegateCommand(SelectGameDataPath);
            ResetGameDataPathCommand = new DelegateCommand(ResetGameDataPath, CanResetGameDataPath);
            RefreshRacesCommand = new DelegateCommand(RefreshRaces);
            SaveRaceSettingsCommand = new DelegateCommand(SaveRaceSettings, CanSaveRaceSettings);
            LoadRaceSettingsCommand = new DelegateCommand(LoadRaceSettings, CanLoadRaceSettings);
            PatchCommand = new AsyncDelegateCommand(Patch, CanPatch);
            SaveConsoleToFileCommand = new DelegateCommand(SaveConsoleToFile);
            AboutCommand = new DelegateCommand(About);
            var al = new GeneralSettingsJsonAL(GeneralSettingsDefaultFile);
            _generalSettings = al.Load();
        }
        #endregion

        #region methods
        public void Initialise()
        {
            try
            {
                PostConsoleMessage(ProgressInfoTypes.Info, "loading user settings");
                var settings = _userSettingsAL.Load();
                foreach (var item in settings.SettingsForVersions)
                {
                    _userSettings.Add(item.Version, item);
                }
                SelectedConsoleLevel = settings.LogLevel;
            }
            catch (Exception ex)
            {
                PostConsoleMessage(ProgressInfoTypes.Error, $"unable to load user settings: {ex.Message}");
            }
            ReadDefaultGameDataPath();
            ApplyUserSettings();
        }
        public void Uninitialise()
        {
            RememberUserSettings();
            try
            {
                _userSettingsAL.Save(new UserSettings() { LogLevel = SelectedConsoleLevel, SettingsForVersions = _userSettings.Values.ToArray() });
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.MainWindow, $"ERROR: unable to save user settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region methods (helping)
        private static bool ComparePaths(string path1, string path2)
        {
            string p1 = Path.GetFullPath(new Uri(path1).LocalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string p2 = Path.GetFullPath(new Uri(path2).LocalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(p1, p2, StringComparison.InvariantCultureIgnoreCase);
        }
        private void UpdateEnabled()
        {
            ResetGameDataPathCommand.RaiseCanExecuteChanged();
            PatchCommand.RaiseCanExecuteChanged();
            SaveRaceSettingsCommand.RaiseCanExecuteChanged();
            LoadRaceSettingsCommand.RaiseCanExecuteChanged();
        }
        private void ClearConsole()
        {
            _consoleText.Clear();
            ConsoleTextCleared?.Invoke(this, EventArgs.Empty);
        }
        private void PostConsoleMessage(ProgressInfoTypes level, string message)
        {
            if (level >= SelectedConsoleLevel)
            {
                _consoleText.Append(level.ToString().ToUpper()).Append(": ").AppendLine(message);
                ConsoleTextChanged?.Invoke(level.ToString().ToUpper(), false);
                ConsoleTextChanged?.Invoke(": ", false);
                ConsoleTextChanged?.Invoke(message, true);
            }
        }
        private void ReadDefaultGameDataPath()
        {
            try
            {
                using (var modding = new Modding(_selectedSkyrimVersion))
                {
                    _gameDataPath = modding.GetGameDataPath();
                    PostConsoleMessage(ProgressInfoTypes.Trace, $"retrieved default game data path {_gameDataPath}");
                }
            }
            catch (Exception ex)
            {
                _gameDataPath = "";
                PostConsoleMessage(ProgressInfoTypes.Error, $"unable to retrieve default game data path: {ex.Message}");
            }
            if (_customGameDataPath is null) RaisePropertyChanged(nameof(GameDataPath));
        }
        private void ApplyUserSettings()
        {
            if(!_userSettings.TryGetValue(SelectedSkyrimVersion, out var settings))
            {
                _customGameDataPath = null;
                OutputName = DefaultOutputName;
                FlagESL = DefaultFlagESL;
                MaxPluginMasters = DefaultMaxPluginMasters.ToString();
                MaxNewRecords = DefaultMaxNewRecords.ToString();
            }
            else
            {
                _customGameDataPath = settings.CustomGameDataPath;
                OutputName = settings.OutputName ?? DefaultOutputName;
                FlagESL = settings.FlagESL ?? true;
                MaxPluginMasters = settings.MaxPluginMasters.HasValue ? settings.MaxPluginMasters.Value.ToString() : DefaultMaxPluginMasters.ToString();
                MaxNewRecords = settings.MaxNewRecords.HasValue ? settings.MaxNewRecords.Value.ToString() : DefaultMaxNewRecords.ToString();
            }
            RefreshRaces();
            if( settings is not null)
            {
                RacesToBeProcessed = Races.Where(r => settings.SelectedRaces.Contains(r.EditorID)).ToArray();
            }
        }
        private void RememberUserSettings()
        {
            bool isNotDefault = false;
            VersionUserSettings settings = new()
            {
                Version = SelectedSkyrimVersion
            };
            if(_customGameDataPath is not null)
            {
                isNotDefault  = true;
                settings.CustomGameDataPath = _customGameDataPath;
            }
            if(OutputName != DefaultOutputName)
            {
                isNotDefault = true;
                settings.OutputName = OutputName;
            }
            if(FlagESL != DefaultFlagESL)
            {
                isNotDefault = true;
                settings.FlagESL = FlagESL;
            }
            if (_maxPluginMastersInt != DefaultMaxPluginMasters)
            {
                isNotDefault = true;
                settings.MaxPluginMasters = _maxPluginMastersInt;
            }
            if (_maxNewRecordsInt != DefaultMaxNewRecords)
            {
                isNotDefault = true;
                settings.MaxNewRecords = _maxNewRecordsInt;
            }
            settings.SelectedRaces = RacesToBeProcessed.Select(r => r.EditorID).ToArray();
            isNotDefault = isNotDefault || settings.SelectedRaces.Any();
            if (isNotDefault)
            {
                _userSettings[SelectedSkyrimVersion] = settings;
            }
            else
            {
                _userSettings.Remove(SelectedSkyrimVersion);
            }
        }

        #region commands
        private void SelectGameDataPath()
        {
            var dlg = new OpenFolderDialog()
            {
                Title = "Select Game Data folder"
            };
            if(_customGameDataPath is not null) dlg.InitialDirectory = _customGameDataPath;
            else if(!string.IsNullOrEmpty(_gameDataPath)) dlg.InitialDirectory = _gameDataPath;
            if(dlg.ShowDialog(Application.Current.MainWindow) == true)
            {
                if(ComparePaths(dlg.FolderName, _gameDataPath))
                {
                    _customGameDataPath = null;
                }
                else
                {
                    _customGameDataPath = dlg.FolderName;
                }
                RaisePropertyChanged(nameof(GameDataPath));
                RefreshRaces();
            }
        }
        private bool CanResetGameDataPath()
        {
            return _customGameDataPath is not null;
        }
        private void ResetGameDataPath()
        {
            if (_customGameDataPath is not null)
            {
                _customGameDataPath = null;
                RaisePropertyChanged(nameof(GameDataPath));
                RefreshRaces();
            }
        }
        private void RefreshRaces()
        {
            try
            {
                PostConsoleMessage(ProgressInfoTypes.Debug, "(re)loading list of custom races");
                foreach (var race in Races)
                {
                    race.UpdateEnabledRequested -= Race_UpdateEnabledRequested;
                }
                using (var modding = new Modding(_selectedSkyrimVersion, GameDataPath))
                {
                    var result = modding.GetRaces();
                    Races = result.RaceEditorIDs.Select(r => new RaceViewModel(r)).ToArray();
                    PostConsoleMessage(ProgressInfoTypes.Debug, $"found {result.RaceEditorIDs.Count()} non-vanilla races, {result.VanillaRacesCount} vanilla races");
                }
                foreach (var race in Races)
                {
                    if (race.TryLoadRaceSettings(RaceSettingsALs, Races))
                    {
                        PostConsoleMessage(ProgressInfoTypes.Trace, $"found and loaded custom race settings for {race.Name}");
                    }
                    else
                    {
                        PostConsoleMessage(ProgressInfoTypes.Debug, $"found no custom race settings for {race.Name}");
                    }
                    race.UpdateEnabledRequested += Race_UpdateEnabledRequested;
                }
            }
            catch(Exception ex)
            {
                Races = [];
                PostConsoleMessage(ProgressInfoTypes.Error, $"unable to refresh races: {ex.Message}");
            }
            RaisePropertyChanged(nameof(GameDataPath));
            RaisePropertyChanged(nameof(Races));
            UpdateEnabled();
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
            if (dlg.ShowDialog(Application.Current.MainWindow) == true)
            {
                string? ext = Path.GetExtension(dlg.FileName);
                if (ext is not null) ext = ext[1..];
                IRaceSettingsAL? al = RaceSettingsALs.FirstOrDefault(a => string.Equals(a.FileExtension, ext));
                if (al is null)
                {
                    PostConsoleMessage(ProgressInfoTypes.Error, $"unknown file extension {ext}");
                    return;
                }
                try
                {
                    SelectedRace.SaveRaceSettings(al);
                }
                catch (Exception ex)
                {
                    PostConsoleMessage(ProgressInfoTypes.Error, $"cannot save race settings: {ex.Message}");
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
            return OutputNameValid && MaxNewRecordsValid && MaxPluginMastersValid && RacesToBeProcessed.Any();
        }
        private async Task Patch()
        {
            ClearConsole();
            string[] filesToBeDeleted;
            try
            {
                var modFileRegex = new Regex(OutputName + @"\d*\.esp", RegexOptions.IgnoreCase);
                filesToBeDeleted = Directory.EnumerateFiles(GameDataPath).Where(f => modFileRegex.IsMatch(f)).Select(f => Path.GetFileName(f)).ToArray();
            }
            catch (Exception ex)
            {
                PostConsoleMessage(ProgressInfoTypes.Error, $"unable to list files to delete: {ex.Message}");
                return;
            }
            if (filesToBeDeleted.Length > 1)
            {
                var dlg = new CheckListDialog()
                {
                    Title = "Select files to delete",
                    ConfirmText = "Delete",
                    ItemsSource = filesToBeDeleted,
                    ItemPreselector = f => true,
                    Owner = Application.Current.MainWindow,
                };
                if (dlg.ShowDialog() != true) return;
                filesToBeDeleted = dlg.SelectedItems.Cast<string>().ToArray();
            }
            else if (filesToBeDeleted.Length == 1)
            {
                if (MessageBox.Show(Application.Current.MainWindow, $"The following file will be deleted: {filesToBeDeleted.First()}", "Confirm Deletion", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel) return;
            }
            IProgress<ProgressInfo> progress = new Progress<ProgressInfo>();
            ((Progress<ProgressInfo>)progress).ProgressChanged += Progress_ProgressChanged;
            _running = true;
            RaisePropertyChanged(nameof(Running));
            await Task.Run(() =>
            {
                Modding? modding = null;
                try
                {
                    // delete old ESPs
                    if (filesToBeDeleted.Length > 0)
                    {
                        progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "deleting old modfile(s)"));
                        foreach (var file in filesToBeDeleted)
                        {
                            progress.Report(new ProgressInfo(ProgressInfoTypes.Info, $"deleting old modfile {file}"));
                            File.Delete(Path.Combine(GameDataPath, file));
                        }
                    }
                    modding = new Modding(SelectedSkyrimVersion, GameDataPath);
                    var processingInfo = new Modding.ArmorProcessingInfo(
                        GameDataPath,
                        OutputName,
                        FlagESL,
                        _maxPluginMastersInt,
                        _maxNewRecordsInt,
                        _generalSettings,
                        _nonWearableArmorRegexes.Select(nar => new Regex(nar, RegexOptions.IgnoreCase)).ToArray()
                    )
                    {
                        Progress = progress
                    };
                    processingInfo.Races = RacesToBeProcessed.Select(r =>
                        new Modding.ArmorRaceProcessingInfo(
                            new Modding.RaceID(r.EditorID),
                            r.HasCustomHeadAA,
                            r.HasCustomBodyAA,
                            r.HasCustomHandsAA,
                            r.HasCustomFeetAA,
                            r.ProcessMale,
                            r.ProcessFemale,
                            r.ReplacerRegexes.Select(rr => new ModelPathRegexInfo(new Regex(rr.SearchRegex, RegexOptions.IgnoreCase), rr.ReplaceString)).ToArray(),
                            r.AdditionalRaces.Select(ar => new Modding.RaceID(ar.EditorID)).ToArray()
                        )).ToArray();
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "processing race(s)"));
                    modding.ProcessRaces(processingInfo);
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Debug, "processed race(s)"));
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "pre processing Armor/ArmorAddons"));
                    var preProcessResult = modding.PreProcessArmor(processingInfo);
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Debug, "pre processed Armor/ArmorAddons"));
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "processing armor"));
                    var result = modding.ProcessArmor(processingInfo, preProcessResult);
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Info, $"processed {result.ArmorCount} armor (overrode {result.OverriddenAACount} ArmorAddons, created {result.NewAACount} new ones, missing {result.MissingPathCount} models)"));
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Info, $"created {result.CreatedFiles.Count()} files"));
                    foreach (var file in result.CreatedFiles)
                    {
                        progress.Report(new ProgressInfo(ProgressInfoTypes.Info, $"-> {file}"));
                    }
                }
                catch (Exception ex)
                {
                    progress.Report(new ProgressInfo(ProgressInfoTypes.Error, $"patch process aborted: {ex}"));
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
        private void About()
        {
            new AboutDialog() { Owner = Application.Current.MainWindow }.ShowDialog();
        }
        #endregion
        #endregion

        #region event handlers
        private void Progress_ProgressChanged(object? sender, ProgressInfo e)
        {
            App.Current.MainWindow?.Dispatcher.BeginInvoke(() =>
            {
                PostConsoleMessage(e.Type, e.Message);
            });
        }
        private void Race_UpdateEnabledRequested(object? sender, EventArgs e)
        {
            UpdateEnabled();
        }
        #endregion
    }

    public delegate void ConsoleTextEventHandler(string msg, bool newline);
}
