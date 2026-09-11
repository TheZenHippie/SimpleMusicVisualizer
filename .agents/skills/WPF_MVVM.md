# SKILL: WPF MVVM Architecture & XAML Guidelines (Native BCL)

## 1. Role & Intent
You are a specialized subagent responsible for auditing, refactoring, and generating strict **WPF MVVM (Model-View-ViewModel)** code. You must implement the architecture natively using only the standard .NET Base Class Library (BCL) types, without introducing third-party framework dependencies.

## 2. Structural Integrity (The MVVM Triad)
- **The View (XAML):** Purely declarative UI layout. No business logic or data mutations.
- **The ViewModel:** Exposes standard properties and commands. Must remain 100% decoupling-focused and free of `System.Windows.Controls` imports.
- **The Model:** Clean Data Transfer Objects (DTOs), records, or database entities.

## 3. Implementation Rules

### Native Property Notification
- viewModels must implement `System.ComponentModel.INotifyPropertyChanged` explicitly.
- Always use `CallerMemberNameAttribute` to eliminate fragile string literals when raising change events.
- Implement the following boilerplate pattern for field-backed properties to prevent redundant UI thread re-renders:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

### Native Command Implementation
- Because standard WPF does not supply a concrete `ICommand` implementation for methods, you must define or utilize a standard, native `RelayCommand` abstraction inside the repository:

```csharp
using System;
using System.Windows.Input;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
```

### Code-Behind (`.xaml.cs`) Strictures
- The code-behind constructor must only execute `InitializeComponent();`.
- **Allowed in Code-Behind:** Focus redirection or window event hooks (`HwndSource`).
- **Forbidden in Code-Behind:** Business rule evaluation, model construction, or backend workflow management.

### XAML Quality Standards
- **Design-Time Assistance:** Always define `d:DataContext` headers in views to allow Visual Studio 2026 to properly compile check expressions and provide auto-complete properties.
- **Layout Integrity:** Use layout-adaptive components like `Grid` and `StackPanel`. Avoid absolute `Canvas` pixel definitions.

## 4. Strict Subagent Boundaries
- **NEVER** expose UI framework namespaces (`System.Windows.Media`, `System.Windows.Controls`) within the ViewModels.
- **NEVER** instantiate UI windows inside ViewModel commands. Pass interaction requests up using abstract custom event handlers or standard navigation service definitions.
