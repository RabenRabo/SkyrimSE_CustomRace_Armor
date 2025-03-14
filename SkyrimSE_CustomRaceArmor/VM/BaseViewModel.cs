using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SSE.CRA.VM
{
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        #region events
        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion

        #region methods (helping)
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        public delegate void DialogResultEventHandler(bool result);
    }
}
