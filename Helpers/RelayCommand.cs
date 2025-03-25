using System;
using System.Windows.Input;

namespace CapnoAnalyzer.Helpers
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _executeWithParameter;
        private readonly Action _executeWithoutParameter;
        private readonly Func<object, bool> _canExecute;

        // Parametreli constructor
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _executeWithParameter = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // Parametresiz constructor
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _executeWithoutParameter = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute == null ? (Func<object, bool>)null : new Func<object, bool>(_ => canExecute());
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            if (_executeWithParameter != null)
            {
                _executeWithParameter(parameter);
            }
            else
            {
                _executeWithoutParameter?.Invoke();
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
