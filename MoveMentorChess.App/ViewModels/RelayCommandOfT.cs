using System.Windows.Input;

namespace MoveMentorChess.App.ViewModels;

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> execute;
    private readonly Func<T?, bool>? canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (parameter is T typedParameter)
        {
            return canExecute?.Invoke(typedParameter) ?? true;
        }

        if (parameter is null && default(T) is null)
        {
            return canExecute?.Invoke(default) ?? true;
        }

        return false;
    }

    public void Execute(object? parameter)
    {
        if (parameter is T typedParameter)
        {
            execute(typedParameter);
        }
        else if (parameter is null && default(T) is null)
        {
            execute(default);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
