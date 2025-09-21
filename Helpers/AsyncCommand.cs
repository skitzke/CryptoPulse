using System.Windows.Input;

namespace CryptoPulse.Helpers;

// Simple async version of ICommand so we can bind async methods to buttons etc.
public class AsyncCommand : ICommand
{
    private readonly Func<object?, Task> _execute;          // the async action to run
    private readonly Func<object?, bool>? _canExecute;      // optional condition check
    private bool _isExecuting;                              // prevent double-click spam

    public AsyncCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    // standard ICommand event — UI listens for this to refresh button enabled state
    public event EventHandler? CanExecuteChanged;

    // only allow execution if not already running + optional check
    public bool CanExecute(object? parameter)
        => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    // actual execution — marks busy, runs async, then resets
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged(); // disable button while running
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged(); // re-enable button when done
        }
    }

    // helper to manually tell UI "hey, check CanExecute again"
    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
