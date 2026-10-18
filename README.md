# LABZ

LABZ is a personal Windows desktop app for Sérgio. It turns a plain-text hardware idea in Portuguese into a real, validated, simulated design. The app is built as modules on a shared core. v0 is Core plus the Circuits module.

**Status:** M1 adds unit-safe quantities and the DC engine. Schematics, ngspice, and Grok are later milestones.

## Prerequisites

- Windows 10 or 11, x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

The WPF shell runs on Windows. The project sets `EnableWindowsTargeting` so the solution can also compile on non-Windows machines; running the app still requires Windows.

## Build, test, and run

From the repository root:

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release --filter "Category!=Spice"
dotnet run --project src/Labz.App/Labz.App.csproj -c Release
```

Spice tests tagged `[Trait("Category", "Spice")]` are excluded only by the filtered command (`dotnet test --filter "Category!=Spice"`), not by plain `dotnet test`. Those tests arrive with ngspice in M3. CI runs the same build and the filtered test command on `windows-2025`.

## Repository layout

```
labz/
├─ src/
│  ├─ Labz.App/                  # WPF shell, pt-BR strings in .resx
│  ├─ Labz.Core.Units/
│  ├─ Labz.Core.Project/
│  ├─ Labz.Core.Engine/
│  ├─ Labz.Core.Drawing/
│  └─ Labz.Modules.Circuits/
├─ tests/
│  ├─ Labz.Core.Units.Tests/
│  ├─ Labz.Core.Engine.Tests/
│  ├─ Labz.Modules.Circuits.Tests/
│  └─ Labz.Architecture.Tests/   # CORE-7: core assemblies must not reference WPF
├─ native/ngspice/x64/           # ngspice.dll + BSD-3 LICENSE, added in M3
├─ schemas/                      # Grok and project JSON schemas, later milestones
├─ samples/                      # REF-1, later milestones
└─ docs/                         # supplementary notes; the spec stays at the root
```

`Labz.Core.Assistant` and `Labz.Spice.Tests` are intentionally absent. They arrive in M4 and M3.

## Quantity text

The parser accepts pt-BR decimals (`4,7 kΩ`), plain decimals (`4.7k`), prefixes from femto (`f`) through tera (`T`), RKM codes (`4K7` = 4.7 kΩ), and the Unicode minus (U+2212). `s` is the second. `S` is the siemens. In a time field, `h` is the hour.

A single separator followed by exactly three digits is rejected (`1.000` and `1,000`). That form is ambiguous: the group can be a thousands separator or a decimal fraction. Write `1.00`, `1,00`, `1000`, or use both separators (`1.000,5`).

## Documents

- [SPEC.md](SPEC.md) is the product specification.
- [AGENTS.md](AGENTS.md) is the short context for coding tools.
