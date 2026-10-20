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

The parser accepts pt-BR decimals (`4,7 kΩ`), plain decimals (`4.7k`), prefixes from femto (`f`) through tera (`T`), and the Unicode minus (U+2212). `s` is the second. `S` is the siemens. In a time field, `h` is the hour (no prefix: `2 mh` is rejected), `min` is the minute and `d` is the day. `Wh/day` is the daily-energy unit.

A comma is always the decimal separator, so `1,250` is 1.250, `0,125` is 0.125, and `1,000 Ω` means 1 Ω. A dot is also a decimal separator, except one ambiguous pattern: a non-zero integer of 1–3 digits, a dot, exactly three digits, and no exponent (`1.000`, `12.500`). That pattern is rejected. Use a comma for decimals and do not write thousands separators. A number with both separators, or with more than one separator (`1.234.567`), is rejected. `0.125` and `1.000e3` stay valid.

RKM codes (`4K7`, `4k7`, `2R5`) are accepted only for resistance, capacitance and inductance.

## Documents

- [SPEC.md](SPEC.md) is the product specification.
- [AGENTS.md](AGENTS.md) is the short context for coding tools.
