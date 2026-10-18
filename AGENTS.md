# AGENTS.md: LABZ

Personal Windows desktop app for Sérgio: turns a plain-text hardware idea in Portuguese into a real, validated, simulated design. Built as **modules on a shared core**; **v0 = Core + Circuits**. Repo: `EstiveJobson/labz`.

**Read `SPEC.md` for details** (architecture, data model, Grok contract, REF-1 reference values, acceptance criteria). Never change a number, tolerance or acceptance criterion from SPEC.md.

## Non-negotiable rules

1. **The engine calculates, Grok explains. Grok never does math.** Every number comes from the engine (formulas and solvers unit-tested against hand-calculated reference values). Every number Grok shows must be quoted from engine output by resultId, never computed by Grok. Grok only: text idea → structured JSON proposal; explain engine results, flag anomalies, suggest next steps; propose components and value changes that the user accepts and the engine recalculates. Every Grok proposal is marked "não verificado" until the engine recalculates.
2. **The app generates the schematic by itself.** Schematics are auto-generated and auto-laid-out; the user only reviews and tweaks them. No drawing skill required.
3. **Everything simulates.** Every module, now and later, must have a simulation path.
4. **The UI is in Brazilian Portuguese (pt-BR).** All user-facing strings in `.resx`, pt-BR. Code, docs, commits and PRs in English.
5. **Works offline.** Without API credits or internet, the app still designs, calculates and simulates; only AI features are unavailable.
6. **Heavy is OK.** Correctness beats speed. Long jobs run off the UI thread with progress and cancel.

## Stack

- C# / .NET 10 (LTS), WPF, MVVM (CommunityToolkit.Mvvm).
- ngspice shared library (`ngspice.dll`, x64) via P/Invoke, from `native/ngspice/x64` (BSD-3: keep the notice).
- Grok: default model `grok-4.3`, configurable in Configurações (`grok-4.7` for more complex cases).
- ScottPlot (WPF) for plots (OxyPlot acceptable). System.Text.Json + JsonSchema.Net.
- xUnit + FluentAssertions. ngspice tests tagged `[Trait("Category","Spice")]`.

## Key conventions

- Core libraries (`Labz.Core.Units`, `.Project`, `.Engine`, `.Drawing`, `.Assistant`) have **no WPF / System.Windows reference**; a CI check enforces this.
- Modules implement `IModule` (components, validators, calculators, simulator). v0 module: `Labz.Modules.Circuits`.
- Quantities are unit-safe (adding V to A is an error). Parse pt-BR formats like `"4,7 kΩ"`, `"4.7k"`, `"100 nF"`.
- Every engine result records `resultId`, value, unit, formula ID and input IDs.
- Project = one folder: `project.labz.json` + `results/` cache + `history/` snapshots. Results are never stored in the project file.
- Everything Grok returns is strictly schema-validated (`circuit-proposal.v0`, `change-proposal.v0`); invalid proposals are rejected in full, never partially applied.
- API key: Configurações (Windows DPAPI) or env var `XAI_API_KEY`. Never hardcoded, logged or committed.
- Engine and units fully tested against REF-1 hand calcs; netlists checked against golden files.
- SPEC.md and AGENTS.md at the repo root (no CURSOR.md). Local repo: `C:\Users\Serginho\LABZ`.
- Branch + PR flow.
