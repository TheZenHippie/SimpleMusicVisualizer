using System;
using System.Windows.Input;

namespace SimpleMusicVisualizer.UI;

/// <summary>
/// Modern, lightweight RelayCommand implementation for WPF MVVM command binding.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action execute)
        : this(_ => execute(), null)
    {
    }

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

/// <summary>
/// Generic RelayCommand for typed command parameters.
/// </summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Predicate<T?>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        if (parameter is null && typeof(T).IsValueType)
        {
            return _canExecute?.Invoke(default) ?? true;
        }

        return _canExecute?.Invoke((T?)parameter) ?? true;
    }

    public void Execute(object? parameter)
    {
        if (parameter is null && typeof(T).IsValueType)
        {
            _execute(default);
            return;
        }

        _execute((T?)parameter);
    }

    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
