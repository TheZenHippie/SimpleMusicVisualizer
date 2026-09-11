# AGENTS.md - Antigravity Project Context & Rules

## 1. Environment & Tech Stack
- **IDE:** Visual Studio 2026 (via Google Antigravity VSIX Extension)
- **Language:** C# (Latest / .NET 9+ features)
- **Application Types:** 
  - Windows Console Applications (.NET Core/Modern)
  - Windows Forms (WinForms) Apps (.NET Core-backed)
  - Windows Presentation Foundation (WPF) Apps (.NET Core-backed, XAML)

## 2. Antigravity Agent Execution Policies
- **Task Verification:** When assigned an epic, always generate an interactive plan/checklist using Antigravity's artifact manager before modifying code.
- **Subagent Delegation:** For heavy parallel refactoring or architecture maps, spawn specialized child agents using `invoke_subagent`. Keep child context clean and lean.
- **Terminal Execution:** You are permitted to execute background terminal commands for solution analysis, building, and formatting. Request review before executing destructive terminal commands.

## 3. Core C# Coding Standards
- **Language Features:** Prefer modern C# syntax. Use file-scoped namespaces, pattern matching, `switch` expressions, primary constructors, and collection expressions (`[]`).
- **Asynchronous Programming:** Always use `async`/`await` for I/O-bound operations. Avoid `.Result` or `.Wait()`. Append `Async` to asynchronous method names.
- **Null Safety:** Nullable reference types (NRT) are **ENABLED**. All code must resolve potential null warnings using null-forgiving (`!`), null-coalescing (`??`), or proper defensive checks.
- **Resource Management:** Utilize `using` statements or file-scoped `using` declarations for objects implementing `IDisposable`.

## 4. UI Framework Best Practices

### Windows Presentation Foundation (WPF)
- **Architecture:** Enforce strict **MVVM (Model-View-ViewModel)** separation. 
- **Code-Behind:** Keep View code-behind (`.xaml.cs`) limited strictly to UI-specific logic (e.g., animations or window initialization). Business logic must reside in the ViewModel.
- **Data Binding:** Use `ICommand` or `RelayCommand` for button actions. Implement `INotifyPropertyChanged` properly via clean boilerplate or source generators.
- **Resources:** Define global styles and templates in dedicated ResourceDictionaries rather than inline.

### Windows Forms (WinForms)
- **Architecture:** Keep UI layout logic separate from business logic. Use MVP (Model-View-Presenter) or a lightweight service layer instead of writing database logic directly inside control event handlers (like `button1_Click`).
- **Threading:** Never update UI controls from background threads. Always use `Invoke` or `BeginInvoke` to marshal calls back to the UI thread.
- **Resource Lifecycle:** Explicitly call `.Dispose()` on dynamically created controls or modal `Form.ShowDialog()` instances.

### Console Applications
- **Structure:** Standard modern `Program.cs`. If dependency injection or logging configuration is required, implement the `Microsoft.Extensions.Hosting` generic host pattern.

## 5. Automation & Operational Commands
When compiling, running tests, or inspecting the multi-project MSBuild architecture, execute standard global .NET CLI commands:
- **Build Solution:** `dotnet build`
- **Run Application:** `dotnet run --project <Path-To-Project>`
- **Run Unit Tests:** `dotnet test`
- **Format Code:** `dotnet format`

## 6. Strict Boundaries ("Never Do")
- **NEVER** write inline SQL queries; use parameterized commands or a type-safe ORM/data layer.
- **NEVER** hardcode connection strings or API keys. Reference them via `App.config`, `appsettings.json`, or User Secrets.
- **NEVER** use `async void` except for UI event handlers (e.g., WinForms or WPF click events).
- **NEVER** ignore exceptions with empty `catch` blocks. Always log or rethrow using `throw;` to preserve the stack trace.
