using SSE.CRA.BL;
using System.IO;
using System.Text.Json;

namespace SSE.CRA.AL
{
    internal class GeneralSettingsJsonAL : IGeneralSettingsAL
    {
        #region fields
        private readonly string _file;
        #endregion

        #region ctors
        public GeneralSettingsJsonAL(string file)
        {
            _file = file;
        }
        #endregion

        #region methods
        public GeneralSettings Load()
        {
            if (!File.Exists(_file))
            {
                using (var fs = new FileStream(_file, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    JsonSerializer.Serialize<GeneralSettings>(fs, new());
                }
                return new();
            }
            using (var fs = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return JsonSerializer.Deserialize<GeneralSettings>(fs) ?? new();
            }
        }
        public void Save(GeneralSettings settings)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
