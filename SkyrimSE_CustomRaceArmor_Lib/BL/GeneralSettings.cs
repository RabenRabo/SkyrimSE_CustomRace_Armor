namespace SSE.CRA.BL
{
    public class GeneralSettings
    {
        #region properties
        public HashSet<string> IgnoreEditorIDs { get; set; } = [];
        public HashSet<string> IgnoreModelPathRegexes { get; set; } = [];
        #endregion
    }
}
