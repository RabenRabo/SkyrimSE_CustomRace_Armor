namespace SSE.CRA.AL
{
    public interface IRaceSettingsAL
    {
        #region properties
        string Directory { get; set; }
        string FileExtension { get; }
        string FileFilter { get; }
        #endregion

        #region methods
        bool Exists(string editorID);
        RaceSettings Load(string editorID);
        void Save(string editorID, RaceSettings settings);
        string ConstructFilename(string editorID);
        #endregion
    }

    public class RaceSettings
    {
        #region properties
        public bool CustomHead { get; set; }
        public bool CustomBody { get; set; }
        public bool CustomHands { get; set; }
        public bool CustomFeet { get; set; }
        public bool ProcessMale { get; set; }
        public bool ProcessFemale { get; set; }
        public KeyValuePair<string, string>[] RegexReplacers { get; set; } = [];
        public string[] AdditionalRaces { get; set; } = [];
        public string[] CompatibleArmorRaces { get; set; } = [];
        #endregion
    }
}