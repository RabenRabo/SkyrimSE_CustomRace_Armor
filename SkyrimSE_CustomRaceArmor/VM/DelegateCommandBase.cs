using System.Windows.Input;

namespace SSE.CRA.VM
{
    internal abstract class DelegateCommandBase : ICommand
    {
        #region events
        public event EventHandler? CanExecuteChanged;
        #endregion

        #region methods
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
        public abstract bool CanExecute(object? parameter);
        public abstract void Execute(object? parameter);
        #endregion
    }
    internal class DelegateCommand : DelegateCommandBase
    {
        #region fields
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        #endregion

        #region ctors
        public DelegateCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        #endregion

        #region methods
        public override bool CanExecute(object? parameter)
        {
            return _canExecute is null || _canExecute();
        }
        public override void Execute(object? parameter)
        {
            _execute();
        }
        #endregion
    }

    internal class AsyncDelegateCommand : DelegateCommandBase
    {
        #region fields
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        #endregion

        #region ctors
        public AsyncDelegateCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        #endregion

        #region methods
        public override bool CanExecute(object? parameter)
        {
            return _canExecute is null || _canExecute();
        }
        public override async void Execute(object? parameter)
        {
            await _execute();
        }
        #endregion
    }
}
