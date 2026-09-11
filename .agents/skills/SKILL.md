# SKILL: Modern C# & .NET 9+ Structural Rules

## 1. Intent & Scope
This rule profile defines execution policies for code structural changes, formatting layouts, and architectural transformations in .NET ecosystem targets.

## 2. Advanced C# Syntax Application
- **Syntactic Sugar:** Prefer primary constructors for dependencies, file-scoped namespaces, global imports, collection expressions (`[]`), and switch pattern matching.
- **Type Definitions:** Leverage standard `record` definitions for immutable data transfers (DTOs) instead of bulky boilerplate classes.

## 3. String & Resource Performance
- **String Interpolation:** Use `$` formatting natively. For complex file structures or formatting large text blocks, prefer raw string literals (`"""your text here"""`).
- **Resource Scope:** Enforce file-scoped `using` statements for memory management safety on `IDisposable` connections.

## 4. Multi-Thread Layout Execution
- **Asynchronous Chains:** Use `await foreach` loops for data streaming and pass `CancellationToken` configurations across custom long-running background tasks.
- **UI Safety:** UI updates must dispatch cleanly through context thread marshaling methods (`Control.Invoke` or `Dispatcher.InvokeAsync`).
