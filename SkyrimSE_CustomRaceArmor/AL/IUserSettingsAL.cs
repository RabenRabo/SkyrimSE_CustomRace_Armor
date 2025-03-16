using Mutagen.Bethesda.Skyrim;
using SSE.CRA.BL;

namespace SSE.CRA.AL
{
    public interface IUserSettingsAL
    {
        #region methods
        UserSettings Load();
        void Save(UserSettings userSettings);
        #endregion
    }

    public class UserSettings
    {
        public const ProgressInfoTypes DefaultLogLevel = ProgressInfoTypes.Info;

        #region properties
        public ProgressInfoTypes LogLevel { get; set; } = DefaultLogLevel;
        public VersionUserSettings[] SettingsForVersions { get; set; } = [];
        #endregion
    }

    public class VersionUserSettings
    {
        #region properties
        public SkyrimRelease Version { get; set; }
        public string? CustomGameDataPath { get; set; }
        public string? OutputName { get; set; }
        public string[] SelectedRaces { get; set; } = [];
        public bool? FlagESL {  get; set; }
        public int? MaxPluginMasters { get; set; }
        public int? MaxNewRecords { get; set; }
        #endregion
    }
}
