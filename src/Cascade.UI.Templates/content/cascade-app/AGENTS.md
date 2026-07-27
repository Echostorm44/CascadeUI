# CascadeApp

## AI Assistant Instructions

This is a Cascade UI application. Cascade UI is a C# cross-platform native
UI framework that is NativeAOT-first and GPU-rendered.

### Key patterns
- Components inherit from `Component` and override `Render()` to return a `Node`
- Reactivity is inferred from field usage — no `[Signal]` needed inside components
- Use `readonly` to opt out of reactivity for a field
- Absent nodes are `Node.Empty`, never `null`
- Localization uses typed keys on the `S` class (optional — plain strings always work)

### Code style
- No leading underscore on private fields
- Always use braces, even for single-line blocks
- Guard clauses over nested conditionals
- Async handlers return `Task`, not `async void`

### Build and test
```bash
dotnet build
dotnet test
dotnet publish -c Release   # NativeAOT build
```
