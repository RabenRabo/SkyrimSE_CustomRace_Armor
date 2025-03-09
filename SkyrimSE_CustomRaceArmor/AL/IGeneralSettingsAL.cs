using SSE.CRA.BL;

namespace SSE.CRA.AL
{
    public interface IGeneralSettingsAL
    {
        #region methods
        GeneralSettings Load();
        void Save(GeneralSettings settings);
        #endregion
    }
}
