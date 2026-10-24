# XQuinn

Reflection utilities and string-driven runtime navigation for .NET.

## Documentation

- [Documentation index](Docs/README.md)
- [Runtime Navigator guide](Docs/Runtime.Navigator.md): setup, controllers,
  references, invocation, queries, and limitations.
- [TypeRegister API reference](Docs/TypeRegister.md): registration, lookup,
  generic definitions, naming rules, and duplicate handling.

The guides describe the source at commit `546352d` (`smal bug fixes`), with the
subsequent removal of `partial` from the `TypeRegister` declaration and correction
of a stale lexer termination call. Screenshots are rendered from live, checked
examples rather than manually written output.

## Build

The checked-in project currently targets `net10.0-windows` with the `win-x64`
runtime identifier. Run these commands from the repository root:

```powershell
dotnet build .\XQuinn.csproj -c Debug -f net10.0-windows
dotnet build .\XQuinn.csproj -c Release -f net10.0-windows
```

`Debug` builds the interactive test executable; `Release` builds the library.
Other frameworks mentioned in older notes are not currently enabled in
[the project file](XQuinn.csproj).

## Reproduce the documentation examples

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Docs\Tools\Update-Documentation.ps1
```

This Windows script builds into a temporary directory, runs the documentation
checks, and regenerates the screenshots and their plain-text transcripts. It
uses the installed .NET SDK and Microsoft Edge or Google Chrome; no additional
documentation packages are required. See the [capture rules](Docs/README.md#screenshot-rules).