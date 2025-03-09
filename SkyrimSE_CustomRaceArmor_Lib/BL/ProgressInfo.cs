namespace SSE.CRA.BL
{
    public class ProgressInfo
    {
        #region fields
        public readonly ProgressInfoTypes Type;
        public readonly string Message;
        #endregion

        #region ctors
        public ProgressInfo(ProgressInfoTypes type, string message)
        {
            Type = type;
            Message = message;
        }
        #endregion
    }

    public enum ProgressInfoTypes
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error
    }
}
