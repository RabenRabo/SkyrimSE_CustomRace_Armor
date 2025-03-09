using System.IO;
using System.Text.Json;

namespace SSE.CRA.AL
{
    internal class RaceSettingsJsonAL : IRaceSettingsAL
    {
        #region properties
        public string Directory { get; set; } = "";
        public string FileExtension => "json";
        public string FileFilter => "JSON file";
        #endregion

        #region methods
        public bool Exists(string editorID)
        {
            return File.Exists(Path.Combine(Directory, ConstructFilename(editorID)));
        }
        public RaceSettings Load(string editorID)
        {
            using (var fs = new FileStream(Path.Combine(Directory, ConstructFilename(editorID)), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return JsonSerializer.Deserialize<RaceSettings>(fs) ?? throw new InvalidDataException("empty JSON file?");
            }
        }
        public void Save(string editorID, RaceSettings settings)
        {
            using (var fs = new FileStream(Path.Combine(Directory, ConstructFilename(editorID)), FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                JsonSerializer.Serialize(fs, settings);
            }
        }
        public string ConstructFilename(string editorID)
        {
            return editorID + ".json";
        }
        #endregion
    }
}
