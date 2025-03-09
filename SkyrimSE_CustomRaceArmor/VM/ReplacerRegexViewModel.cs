using System.Runtime.CompilerServices;

namespace SSE.CRA.VM
{
    internal class ReplacerRegexViewModel : BaseViewModel
    {
        #region fields
        private string _searchRegex = "";
        private string _replaceString = "";
        #endregion

        #region properties
        public int Index { get; set; }
        public string SearchRegex
        {
            get => _searchRegex;
            set
            {
                if(_searchRegex != value)
                {
                    _searchRegex = value;
                    RaisePropertyChanged();
                }
            }
        }
        public string ReplaceString
        {
            get => _replaceString;
            set
            {
                if (_replaceString != value)
                {
                    _replaceString = value;
                    RaisePropertyChanged();
                }
            }
        }
        #endregion
    }
}
