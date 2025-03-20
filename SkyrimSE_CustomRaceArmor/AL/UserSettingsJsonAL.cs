using System.IO;
using System.Text.Json;

namespace SSE.CRA.AL
{
    internal class UserSettingsJsonAL : IUserSettingsAL
    {
        #region fields
        private readonly string _filename;
        #endregion

        #region ctors
        public UserSettingsJsonAL(string filename)
        {
            _filename = filename;
        }
        #endregion

        #region methods
        public UserSettings Load()
        {
            if (!File.Exists(_filename)) return new();
            using var fs = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize<UserSettings>(fs) ?? new();
        }
        public void Save(UserSettings userSettings)
        {
            using var fs = new FileStream(_filename, FileMode.Create, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(fs, userSettings);
        }
        #endregion
    }
}
