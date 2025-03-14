﻿using Microsoft.Win32;
using Mutagen.Bethesda.Skyrim;
using SSE.CRA.AL;
using SSE.CRA.BL;
using SSE.CRA.UI;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

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
        private bool _flagESL = true;
        private int _maxNewRecordsInt;
        private string _maxNewRecords = "";
        private int _maxPluginMastersInt;
        private string _maxPluginMasters = "";
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
        public bool OutputNameValid => !string.IsNullOrEmpty(_outputName) && int.TryParse(MaxNewRecords, out int mnr);
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
            SaveRaceSettingsCommand = new DelegateCommand(SaveRaceSettings, CanSaveRaceSettings);
            LoadRaceSettingsCommand = new DelegateCommand(LoadRaceSettings, CanLoadRaceSettings);
            PatchCommand = new AsyncDelegateCommand(Patch, CanPatch);
            SaveConsoleToFileCommand = new DelegateCommand(SaveConsoleToFile);
            var al = new GeneralSettingsJsonAL(GeneralSettingsDefaultFile);
            _generalSettings = al.Load();
            using (var modding = new Modding())
            {
                _gameDataPath = modding.GetGameDataPath();
                _races = modding.GetRaces().Select(pair => new RaceViewModel(pair.Key, pair.Value)).ToArray();
            }
            foreach (var race in _races)
            {
                race.TryLoadingRaceSettings();
                race.PropertyChanged += Race_PropertyChanged;
                race.UpdateEnabledRequested += Race_UpdateEnabledRequested;
            }
            MaxPluginMasters = "200";
            MaxNewRecords = "2000";
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
            return OutputNameValid && MaxNewRecordsValid && MaxPluginMastersValid && _races.Any(r => r.ToBeProcessed);
        }
        //private async Task Patch()
        //{
        //    _consoleText.Clear();
        //    ConsoleTextCleared?.Invoke(this, EventArgs.Empty);
        //    string[] filesToBeDeleted;
        //    try
        //    {
        //        var modFileRegex = new Regex(OutputName + @"\d*\.esp", RegexOptions.IgnoreCase);
        //        filesToBeDeleted = Directory.EnumerateFiles(_gameDataPath).Where(f => modFileRegex.IsMatch(f)).Select(f => Path.GetFileName(f)).ToArray();
        //    }
        //    catch (Exception ex)
        //    {
        //        ConsoleTextChanged?.Invoke($"ERROR: while finding files to delete: {ex.Message}", true);
        //        return;
        //    }
        //    if (filesToBeDeleted.Length > 1)
        //    {
        //        var dlg = new CheckListDialog()
        //        {
        //            Title = "Select files to delete",
        //            ConfirmText = "Delete",
        //            ItemsSource = filesToBeDeleted,
        //            ItemPreselector = f => true
        //        };
        //        if (dlg.ShowDialog() != true) return;
        //        filesToBeDeleted = dlg.SelectedItems.Cast<string>().ToArray();
        //    }
        //    else if (filesToBeDeleted.Length == 1)
        //    {
        //        if (MessageBox.Show($"The following file will be deleted: {filesToBeDeleted.First()}", "Confirm Deletion", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel) return;
        //    }
        //    IProgress<ProgressInfo> progress = new Progress<ProgressInfo>();
        //    ((Progress<ProgressInfo>)progress).ProgressChanged += Progress_ProgressChanged;
        //    _running = true;
        //    RaisePropertyChanged(nameof(Running));
        //    await Task.Run(() =>
        //    {
        //        Modding? modding = null;
        //        try
        //        {
        //            // delete old ESPs
        //            if (filesToBeDeleted.Length > 0)
        //            {
        //                progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "deleting old modfile(s)"));
        //                foreach (var file in filesToBeDeleted)
        //                {
        //                    progress.Report(new ProgressInfo(ProgressInfoTypes.Info, $"deleting old modfile {file}"));
        //                    File.Delete(Path.Combine(_gameDataPath, file));
        //                }
        //            }
        //            modding = new Modding();
        //            progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "removing ArmorRace from race"));
        //            var processingInfo = new Modding.ArmorProcessingInfo(
        //                _gameDataPath,
        //                OutputName,
        //                FlagESL,
        //                _maxPluginMastersInt,
        //                _maxNewRecordsInt,
        //                _generalSettings,
        //                _nonWearableArmorRegexes.Select(nar => new Regex(nar, RegexOptions.IgnoreCase)).ToArray()
        //            );
        //            var races = _races.Where(r => r.ToBeProcessed).Select(r => new KeyValuePair<RaceViewModel, Modding.RaceGetterPair>(r, modding.RemoveArmorRace(processingInfo, r.EditorID, r.VampEditorID, progress))).ToArray();
        //            processingInfo.Races = races.Select(pair =>
        //                new Modding.ArmorRaceProcessingInfo(
        //                    pair.Value,
        //                    pair.Key.HasCustomHeadAA,
        //                    pair.Key.HasCustomBodyAA,
        //                    pair.Key.HasCustomHandsAA,
        //                    pair.Key.HasCustomFeetAA,
        //                    pair.Key.ProcessMale,
        //                    pair.Key.ProcessFemale,
        //                    pair.Key.ReplacerRegexes.Select(vm => new KeyValuePair<Regex, string>(new Regex(vm.SearchRegex, RegexOptions.IgnoreCase), vm.ReplaceString)).ToArray())).ToArray();
        //            progress.Report(new ProgressInfo(ProgressInfoTypes.Debug, "removed ArmorRace from race"));
        //            progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "updating race skin armor"));
        //            int countSkinAAs = modding.SetArmorRaceOfSkin(processingInfo, progress);
        //            progress.Report(new ProgressInfo(ProgressInfoTypes.Debug, $"updated race skin armor ({countSkinAAs} ArmorAddons updated)"));
        //            progress.Report(new ProgressInfo(ProgressInfoTypes.Trace, "processing armor"));
        //            var result = modding.ProcessArmor(processingInfo, progress, null);
        //            progress.Report(new ProgressInfo(ProgressInfoTypes.Info, $"processed {result.ArmorCount} armor (overrode {result.OverwriteAAs} ArmorAddons, created {result.NewAAs} new ones, missing {result.MissingPaths} models)"));
        //            progress.Report(new ProgressInfo(ProgressInfoTypes.Info, $"created {result.CreatedFiles.Count()} files"));
        //            foreach (var file in result.CreatedFiles)
        //            {
        //                progress.Report(new ProgressInfo(ProgressInfoTypes.Info, $"-> {file}"));
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            progress.Report(new ProgressInfo(ProgressInfoTypes.Error, $"patch process aborted: {ex}"));
        //        }
        //        finally
        //        {
        //            modding?.Dispose();
        //        }
        //    });
        //    _running = false;
        //    RaisePropertyChanged(nameof(Running));
        //}
        private async Task Patch()
        {
            _consoleText.Clear();
            ConsoleTextCleared?.Invoke(this, EventArgs.Empty);
            string[] filesToBeDeleted;
            try
            {
                var modFileRegex = new Regex(OutputName + @"\d*\.esp", RegexOptions.IgnoreCase);
                filesToBeDeleted = Directory.EnumerateFiles(_gameDataPath).Where(f => modFileRegex.IsMatch(f)).Select(f => Path.GetFileName(f)).ToArray();
            }
            catch (Exception ex)
            {
                ConsoleTextChanged?.Invoke($"ERROR: while finding files to delete: {ex.Message}", true);
                return;
            }
            if (filesToBeDeleted.Length > 1)
            {
                var dlg = new CheckListDialog()
                {
                    Title = "Select files to delete",
                    ConfirmText = "Delete",
                    ItemsSource = filesToBeDeleted,
                    ItemPreselector = f => true
                };
                if (dlg.ShowDialog() != true) return;
                filesToBeDeleted = dlg.SelectedItems.Cast<string>().ToArray();
            }
            else if (filesToBeDeleted.Length == 1)
            {
                if (MessageBox.Show($"The following file will be deleted: {filesToBeDeleted.First()}", "Confirm Deletion", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel) return;
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
                            File.Delete(Path.Combine(_gameDataPath, file));
                        }
                    }
                    modding = new Modding();
                    var processingInfo = new Modding.ArmorProcessingInfo(
                        _gameDataPath,
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
                    processingInfo.Races = _races.Where(r => r.ToBeProcessed).Select(r =>
                        new Modding.ArmorRaceProcessingInfo(
                            Modding.RaceIDPair.FromEditorIDs(r.EditorID, r.VampEditorID),
                            r.HasCustomHeadAA,
                            r.HasCustomBodyAA,
                            r.HasCustomHandsAA,
                            r.HasCustomFeetAA,
                            r.ProcessMale,
                            r.ProcessFemale,
                            r.ReplacerRegexes.Select(rr => new ModelPathRegexInfo(new Regex(rr.SearchRegex, RegexOptions.IgnoreCase), rr.ReplaceString)).ToArray()
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
