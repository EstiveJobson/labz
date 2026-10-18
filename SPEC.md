# SPEC.md: LABZ, v0: Core + Circuits Module

> **Status:** DRAFT v0, 2026-10-18. Planned by Planino with Sérgio.
> **Audience:** Sérgio, Promptino (build prompts), Testino (tests), AI coding tools (Cursor / Grok Build).
> **Note:** Personal project, not for sale for now. **App name:** LABZ.
> **Repo:** GitHub `EstiveJobson/labz`. Short rules for AI tools: see `AGENTS.md`.
> Section numbers (§2–§11) match the original draft so cross-references (e.g. §6.2, §9.6) stay valid.

## Overview

Sérgio has lots of hardware ideas but cannot draw. LABZ turns a plain-text idea in Portuguese into a real, validated, simulated design. The long-term goal is every kind of hardware, from circuits to propulsion. It is deliberately huge, so it is built as **modules on a shared core**. **v0 = Core + Circuits.**

Windows desktop app: C# / .NET 10 (LTS), WPF, MVVM, embedded ngspice, Grok (xAI API) as assistant only.

---

## 1. Non-negotiable rules

These override everything else. If code or a prompt would break one of them, stop and ask.

| # | Rule | What it means in code |
|---|---|---|
| R1 | **The engine calculates, Grok explains. Grok never does math.** | Every number comes from the engine: formulas and solvers that are unit-tested against hand-calculated reference values. Every number Grok shows must be quoted from engine output, never computed by Grok. Grok must never produce computed values. Grok proposals are marked "não verificado" until the engine recalculates (D5). |
| R2 | **No drawing skill required: the app generates the schematic by itself.** | Schematics are auto-generated and auto-laid-out. The user only reviews and tweaks them. |
| R3 | **Everything simulates.** | Every module, now and later, must have a simulation path. |
| R4 | **The UI is in Brazilian Portuguese (pt-BR).** | All user-facing strings, messages and errors are pt-BR (strings in `.resx`). Docs, commits and PRs are in English. |
| R5 | **Heavy is OK.** | Slow, CPU-heavy calculations are acceptable. Correctness beats speed. Long jobs run off the UI thread with progress and cancel. |
| R6 | **Works offline.** | Without API credits or internet, the app still designs, calculates and simulates. Only the AI features are unavailable. |

**What Grok (xAI API) is allowed to do, and nothing more:**
- turn text ideas into a structured proposal (JSON);
- read engine results, explain them, flag anomalies and suggest next steps;
- propose value changes, which the user accepts and the engine then recalculates.

Related enforcement: AI-5 (system prompt forbids computing numbers), AI-6 / §7.4 (numbers in Grok replies not matching an engine result within 0.5% are marked "não verificado"), CORE-7 (engine assemblies have no WPF reference).

---

## 2. Architecture

```
┌──────────────────────────── WPF App (pt-BR UI, MVVM) ────────────────────────────┐
│  Project workspace │ Drawing canvas │ Results/plots │ Grok chat panel            │
└──────────────┬─────────────────────────────────────────────────────┬────────────┘
               │                                                     │
┌──────────────▼──────────── CORE (class libraries, no UI) ──────────▼────────────┐
│ Labz.Core.Units      SI quantities, prefixes, unit-safe arithmetic                │
│ Labz.Core.Project    project model, JSON (de)serialization, versioning            │
│ Labz.Core.Engine     formula registry, solvers (linear/nonlinear), result store   │
│ Labz.Core.Drawing    canvas model, auto-layout, routing (UI-agnostic geometry)    │
│ Labz.Core.Assistant  Grok client, prompt building, JSON schema validation         │
└──────────────┬───────────────────────────────────────────────────────────────────┘
               │ module contract (IModule: components, validators, calculators, simulator)
┌──────────────▼──────────┐  later: Mechanics & Structures · Thermal · Fluids & Propulsion
│ Labz.Modules.Circuits    │
│  library, netlist gen,  │
│  energy calcs, ngspice  │
└─────────────────────────┘
```

**Core**
- **Project workspace:** one project = one folder with `project.labz.json`, plus a `results/` cache and `history/` snapshots.
- **Units:** SI base units with prefixes (p, n, µ, m, k, M, G, T). Quantities carry their dimension, so adding V to A is a compile-time or run-time error. Parsing accepts the pt-BR formats `"4,7 kΩ"`, `"4.7k"` and `"100 nF"`.
- **Engine:** a registry of named, pure formulas plus solvers. Every result records its formula ID and inputs, so it can be traced and explained.
  - **Switches:** ideal in the engine (open/closed). `Ron` and `Roff` exist only in the SPICE path (M3).
  - **Diode model:** Shockley equation solved with Newton's method.
  - **LED model:** fixed forward-voltage drop.
  - **Charge efficiency:** fixed at 1.0 in v0 (not in the project JSON); becomes a parameter in a future version.
- **Drawing canvas:** a geometry model for symbols, pins, wires and labels, plus auto-layout. Rendering lives in WPF.
- **Assistant layer:** the Grok client, the context builder (which feeds Grok engine results only) and strict schema validation of everything Grok returns.

**Modules:** Circuits (v0). Later, only listed here: Mechanics & Structures, Thermal, Fluids & Propulsion.

---

## 3. Circuits module v0: scope

- Text idea → Grok proposal (components + connections) → validation → auto-generated schematic.
- Schematic editing: drag components, edit values, add or remove parts and wires, undo/redo.
- Netlist generation from the schematic (SPICE format).
- Simulation through embedded **ngspice** (shared library `ngspice.dll`, P/Invoke):
  - DC operating point (`.op`);
  - DC sweep (`.dc`);
  - AC small-signal (`.ac`);
  - transient (`.tran`).
- Results:
  - plots (V and I against time, sweep variable or frequency, Bode magnitude and phase);
  - a per-component table of voltage across, current through and power.
- **Energy calculations (engine, independent of ngspice):**
  - Ohm's law and power for each component;
  - resistor dissipation checked against the power rating;
  - consumption in Wh per day from a usage profile (hours per day);
  - battery runtime in hours;
  - charge current and time to recharge.

### 3.1 Must-have (v0)
- Units library and project JSON save/open.
- Component library v0 (§6.2).
- Text → Grok → validated proposal → auto-laid-out schematic.
- Schematic editing with recalculation.
- Netlist generation and ngspice for OP, DC sweep, AC and transient.
- Per-component V, I and P table, and plots.
- Energy calculations and the power-rating check.
- Grok "explain results" and "propose change → accept → recalc".
- Offline mode.
- Reference project REF-1 passing all tests.

### 3.2 Nice-to-have (v0.x)
- Better routing (fewer wire crossings) and orthogonal wire beautifier.
- Export the schematic as PNG, SVG or PDF, and results as CSV.
- Parameter sweep wizard ("what if R = 100…330 Ω").
- Single-diode photovoltaic panel model (I–V curve, MPP).
- Battery state-of-charge transient model.
- E-series value snapping (E12/E24) with suggestions.
- Dark and light themes.

### 3.3 Out of scope (v0)
- PCB layout, Gerber, BOM pricing.
- Digital, mixed-signal or microcontroller simulation.
- Importing vendor SPICE models in bulk (single-model import can come later).
- Other modules (mechanics, thermal, fluids/propulsion).
- Multi-user, cloud sync, macOS/Linux.

---

## 4. Screens and user flows (UI in pt-BR)

**Screens**
1. **Início:** recent projects, "Novo projeto a partir de uma ideia" and "Abrir".
2. **Ideia:** a large text box, the Grok proposal preview (component list and connections), and the buttons "Gerar esquema" and "Pedir outra proposta".
3. **Esquema:** the canvas, a properties panel for the selected part, a library sidebar and the validation messages.
4. **Simulação:** pick the analysis (OP, DC, AC, Transiente), its parameters, and "Simular", with a progress bar and cancel.
5. **Resultados:** plots, the per-component table, energy cards (Wh/dia, autonomia, corrente de carga) and warnings.
6. **Assistente (side panel):** chat with Grok, showing proposals as accept/reject cards.
7. **Configurações:** xAI API key, model (default `grok-4.3`; `grok-4.7` offered for more complex cases; see §7.0), units display and autosave (interval 1 to 60 min, default 2 min).

**Flows**
- **F1, new project from an idea:**
  1. Sérgio types the idea in Portuguese.
  2. Grok returns a proposal as JSON.
  3. The validator accepts it or shows the reasons it was rejected, and Grok is asked to retry once automatically.
  4. Auto-layout runs and the schematic is shown.
  5. The engine runs the OP and energy calculations.
- **F2, review and edit:** drag or edit a value → the model updates → the engine recalculates (debounced) → the table and warnings refresh.
- **F3, simulate:** choose the analysis → generate the netlist → ngspice runs in the background → results are parsed and plotted.
- **F4, ask Grok:** "Por que o resistor esquenta?" → Grok gets the project plus engine results → it answers, quoting engine numbers by result ID.
- **F5, accept a proposal:**
  1. Grok proposes `{change R1 150 Ω → 180 Ω, reason}`.
  2. A card shows the diff.
  3. Accepting it applies the change, saves an undo snapshot and has the engine recalculate.
  4. The new numbers come from the engine.
- **F6, save/open:** Ctrl+S writes `project.labz.json`. Opening a file validates its schema version and migrates it if needed.

---

## 5. Data and integrations

- **xAI Grok API** (default model `grok-4.3`, configurable in Configurações; see §7.0). The key comes from Configurações (stored with Windows DPAPI) or from the env var `XAI_API_KEY`. It is never hardcoded or committed.
- **ngspice** shared library, bundled with the app (BSD-3 licence, so keep the notice).
- No other external services.

---

## 6. Data model

### 6.1 Project JSON (`project.labz.json`)
```json
{
  "schemaVersion": "0.1",
  "id": "uuid",
  "name": "Carregador solar 12 V",
  "createdAt": "2026-10-18T16:00:00-03:00",
  "idea": "texto original do Sérgio",
  "modules": {
    "circuits": {
      "components": [
        { "id": "R1", "type": "resistor",
          "params": { "resistance": "150 Ω", "powerRating": "0.25 W" },
          "pins": ["1", "2"],
          "layout": { "x": 320, "y": 160, "rotation": 0 } }
      ],
      "nets": [
        { "id": "N_BAT", "connections": [ ["BAT1","+"], ["R1","1"] ] },
        { "id": "0", "connections": [ ["GND","g"], ["BAT1","-"] ] }
      ],
      "usageProfiles": [ { "target": "LOAD", "hoursPerDay": 6 } ],
      "analyses": [ { "type": "op" }, { "type": "tran", "step": "1 ms", "stop": "5 s" } ]
    }
  },
  "history": [ { "at": "...", "source": "user|grok", "change": "R1.resistance 150→180 Ω" } ]
}
```
Peak-sun hours are stored as a `usageProfiles` entry whose `target` equals the panel's id (e.g. `PV1`). Charge efficiency is not stored in the project JSON (fixed at 1.0 in v0).

Results are not stored in the project file. They are cached in `results/` and can always be regenerated.

### 6.2 Component library v0
| Type | Pins | Params (units) | SPICE emission |
|---|---|---|---|
| `resistor` | 1,2 | resistance (Ω), powerRating (W, default 0.25) | `R` |
| `capacitor` | 1,2 | capacitance (F), initialVoltage (V, opt) | `C` |
| `inductor` | 1,2 | inductance (H), initialCurrent (A, opt) | `L` |
| `diode` | A,K | model: IS, N, RS, BV (defaults: 1N4148-like); Schottky preset | `D` + `.model` |
| `led` | A,K | forwardVoltage (V), forwardCurrent (A), color; fitted IS/N/RS so Vf(If) matches | `D` + `.model` |
| `dc_voltage_source` | +,− | voltage (V) | `V` |
| `solar_panel` | +,− | v0 Thevenin model: openCircuitVoltage (V), seriesResistance (Ω); info only: ratedPower (W) | `V` + `R` subcircuit |
| `battery` | +,− | nominalVoltage (V), internalResistance (Ω), capacity (Ah), usableDepthOfDischarge (0–1) | `V` + `R` subcircuit |
| `switch` | 1,2 | state (open/closed), Ron (Ω, default 1 mΩ), Roff (Ω, default 1 GΩ) | `R` (state-dependent); engine treats the switch as ideal, Ron/Roff used only in SPICE (M3) |
| `ground` | g | — | node `0` |

---

## 7. Grok integration contract

### 7.0 Model
- **Default model:** `grok-4.3` (US$1.25 per 1M input tokens, US$2.50 per 1M output tokens, 1M-token context).
- The model is configurable in Settings (**Configurações** in the pt-BR UI). `grok-4.7` is offered as an option for more complex cases.

### 7.0a Proposals are "not verified" until recalculated
Grok may propose components and values, but every proposal is marked "not verified" (pt-BR UI label: **"não verificado"**) until the engine recalculates. Grok never shows a result it calculated itself.

### 7.1 Proposal: idea → circuit (Grok output schema `circuit-proposal.v0`)
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "circuit-proposal.v0",
  "type": "object",
  "required": ["summary", "components", "connections"],
  "additionalProperties": false,
  "properties": {
    "summary": { "type": "string", "maxLength": 600 },
    "assumptions": { "type": "array", "items": { "type": "string" } },
    "components": { "type": "array", "minItems": 1, "items": {
      "type": "object", "required": ["id", "type", "params"], "additionalProperties": false,
      "properties": {
        "id": { "type": "string", "pattern": "^[A-Z]{1,4}[0-9]{1,3}$" },
        "type": { "enum": ["resistor","capacitor","inductor","diode","led","dc_voltage_source","solar_panel","battery","switch","ground"] },
        "params": { "type": "object", "additionalProperties": { "type": "string" } },
        "role": { "type": "string" } } } },
    "connections": { "type": "array", "items": {
      "type": "object", "required": ["net", "pins"], "additionalProperties": false,
      "properties": {
        "net": { "type": "string" },
        "pins": { "type": "array", "minItems": 2, "items": { "type": "string", "pattern": "^[A-Z]{1,4}[0-9]{1,3}\\.[A-Za-z0-9+\\-]+$" } } } } }
  }
}
```
Param values are strings with units (e.g. `"150 Ω"`) and are parsed by `Labz.Core.Units`.

### 7.2 Change proposal (Grok output schema `change-proposal.v0`)
```json
{ "changes": [ { "target": "R1.resistance", "from": "150 Ω", "to": "180 Ω", "reason": "string" } ],
  "explanation": "string" }
```

### 7.3 Explain request (app → Grok input)
- The project JSON, without layout.
- The engine results as a list of `{ "resultId": "R1.P", "value": 0.0592, "unit": "W", "formula": "P=I²R" }`.
- The warnings and the user question.
- The system prompt states:
  - "Never compute numbers. Quote only values from `results`, citing their resultId. If a needed number is missing, ask the app to compute it."

### 7.4 Validation and rejection
In order, the app checks:
1. The JSON parses.
2. It matches the schema.
3. Component types are known.
4. Params parse with the correct dimension.
5. Every pin referenced exists and is connected at most once.
6. Exactly one ground net, named `0`.
7. No floating nodes.
8. No voltage-source loops without resistance.
9. Values are inside sane ranges, e.g. 1 mΩ ≤ R ≤ 1 TΩ.

If any check fails, the proposal is rejected with a list of human-readable pt-BR reasons and is never partially applied. The app retries once automatically (1 automatic retry, fixed in v0), sending the reasons back to Grok, then shows them to the user.

When Grok's explanation text contains a number, the app checks it against the engine results. A number that does not match any result (within 0.5% after unit normalization) is highlighted as "não verificado".

### 7.5 Offline / no credits
- No key, a network error, HTTP 401/402/429 or a timeout of 60 s or more makes the Assistant panel show a clear pt-BR status, e.g. "Assistente indisponível: sem créditos/sem conexão".
- Everything else keeps working.
- The idea flow falls back to "start from template" (REF-1 and a blank schematic) or manual building from the library.

---

## 8. Stack and repo layout

- **C# / .NET 10 (LTS)**, WPF, MVVM (CommunityToolkit.Mvvm).
- **ngspice** shared library (`ngspice.dll`, x64) via P/Invoke, with callbacks for output and background-thread status.
- **Plotting:** ScottPlot (WPF control). OxyPlot is acceptable instead.
- **JSON:** System.Text.Json, with schema validation through JsonSchema.Net.
- **Tests:** xUnit and FluentAssertions. The engine and units are fully tested. ngspice integration tests are tagged `[Trait("Category","Spice")]`.
- **Repo:** GitHub `EstiveJobson/labz`, with a branch + PR flow. Docs, commits and PRs are in English, and the UI is in pt-BR (strings in `.resx`).

```
labz/
├─ README.md                 # what/why, build steps
├─ SPEC.md                   # full spec (this file)
├─ AGENTS.md                 # short context for AI tools
├─ src/
│  ├─ Labz.App/               # WPF, views, viewmodels, resx pt-BR
│  ├─ Labz.Core.Units/
│  ├─ Labz.Core.Project/
│  ├─ Labz.Core.Engine/
│  ├─ Labz.Core.Drawing/
│  ├─ Labz.Core.Assistant/
│  └─ Labz.Modules.Circuits/  # library, netlist, energy, Spice interop
├─ native/ngspice/x64/       # ngspice.dll + LICENSE
├─ schemas/                  # circuit-proposal.v0.json, change-proposal.v0.json, project.v0.json
├─ samples/REF-1-solar-led/project.labz.json
└─ tests/
   ├─ Labz.Core.Units.Tests/
   ├─ Labz.Core.Engine.Tests/
   ├─ Labz.Modules.Circuits.Tests/   # REF-1 hand-calc tests, netlist golden files
   └─ Labz.Spice.Tests/              # ngspice vs hand-calc
```
SPEC.md and AGENTS.md live at the repo root (no CURSOR.md, no docs/spec file). Local repo: `C:\Users\Serginho\LABZ`; `EstiveJobson/labz` will be pushed to GitHub later.

Engine projects reference no WPF assemblies, and a CI check enforces this.

---

## 9. Reference project REF-1: solar panel → 12 V battery → LED strip

> **All values below are EXAMPLE VALUES chosen for the test**, not real part specs. They are used to prove that the engine calculates correctly.

### 9.1 Circuit and values
- **PV1 solar panel (v0 Thevenin model):** Voc = 18.0 V, Rs = 5.0 Ω, for a "20 W class" panel. This gives Isc = 3.6 A.
- **BAT1 battery:** 12.0 V nominal EMF, Rint = 0.10 Ω, 7.0 Ah, usable DoD = 50% (lead-acid style).
- **LED strip:** 10 parallel branches. Each branch has 3 white LEDs in series (Vf = 3.0 V at 20 mA each, fixed-drop model in hand calcs) and a current-limiting resistor R = 150 Ω rated 0.25 W.
- **S1** connects the panel ("dia"), and **S2** switches the load on.
- **Usage:** LEDs on 6 h/day, and 5 peak-sun hours/day.
  - *Decision D9:* peak-sun hours are stored as a `usageProfiles` entry with `target` = panel id (`PV1`).
- Charge efficiency is 1.0 for this test (a parameter, default 1.0 in v0).
  - *Decision D8:* in v0 charge efficiency is fixed at 1.0 and is not in the project JSON; it becomes a parameter in a future version.

```
 PV1(18V)──Rs 5Ω──S1──┬── N_BAT ──S2──┬──[150Ω]──▶|▶|▶|── 0   (×10 branches)
                      │               └── ... 
                 BAT1 (12V + 0.1Ω)
                      │
                      0 (GND)
```
The load is equivalent to a 9.0 V drop in series with 150/10 = **15 Ω**, valid while each LED is conducting.

### 9.2 Scenario A: night (S1 open, S2 closed), battery → LEDs
- Total current: I = (12.0 − 9.0) / (15 + 0.1) = 3 / 15.1 = **0.198675 A**
- Battery terminal voltage: Vt = 12.0 − 0.1 × 0.198675 = **11.980132 V**
- Per-branch current: Ib = 0.0198675 A (**19.87 mA**)
- Resistor power: P_R = Ib² × 150 = **0.059208 W**. That is 23.7% of the 0.25 W rating → **OK** (the warning threshold is 50% of rating, and the error threshold is 100%).
- LED power per branch: 3 × 3.0 × Ib = **0.178808 W**
- Load power: P_load = Vt × I = **2.380159 W**
- Battery internal loss: I² × 0.1 = **0.003947 W**
- Power drawn from the battery EMF: 12.0 × I = **2.384106 W**. Check: 2.380159 + 0.003947 = 2.384106 ✓
- Energy per day: 2.384106 W × 6 h = **14.3046 Wh/day**
- Usable capacity: 7.0 Ah × 0.5 = 3.5 Ah, or 42.0 Wh at 12 V
- Runtime (continuous): 3.5 Ah / 0.198675 A = **17.617 h**
- Autonomy: 42.0 / 14.3046 = **2.936 days**

### 9.3 Scenario B: day, charge only (S1 closed, S2 open)
- Charge current: Ich = (18.0 − 12.0) / (5.0 + 0.1) = 6 / 5.1 = **1.176471 A**
- Battery terminal voltage: Vt = 12.0 + 0.1 × 1.176471 = **12.117647 V**
- Panel output power at the terminals: Vt × Ich = **14.256055 W**
- Power stored (into the EMF): 12.0 × Ich = **14.117647 W**
- Panel internal (Rs) loss: Ich² × 5 = **6.920415 W**
- Source total: 18 × Ich = **21.176471 W**. Check: 14.256055 + 6.920415 = 21.176470 ✓
- Charge energy per day: 14.117647 W × 5 h = **70.588 Wh/day**
- Time to recharge the usable 3.5 Ah: 3.5 / 1.176471 = **2.975 h**

### 9.4 Scenario C: day, charging with the load on (S1 and S2 closed): nodal/MNA test
The node equation at N_BAT (V) is:
(18 − V)/5 + (12 − V)/0.1 = (V − 9)/15

Multiplying by 15: 54 − 3V + 1800 − 150V = V − 9, so 1863 = 154 V.
- Node voltage: V = 1863/154 = **12.097403 V**
- Panel current: (18 − V)/5 = **1.180519 A**
- Load current: (V − 9)/15 = **0.206494 A**, which is **20.65 mA** per branch
- Battery charge current: (V − 12)/0.1 = **0.974026 A**. KCL check: 1.180519 = 0.206494 + 0.974026 ✓
- Resistor power per branch: 0.0206494² × 150 = **0.063959 W** → OK
- Power balance:
  - panel EMF: 18 × 1.180519 = **21.249351 W**;
  - Rs loss: **6.968131 W**;
  - load: V × 0.206494 = **2.498035 W**;
  - stored: 12 × 0.974026 = **11.688312 W**;
  - Rint loss: **0.094873 W**.
  The parts sum to 21.249351 W ✓

### 9.5 Extra simulation references (generic, closed-form)
- **Transient RC:** a 12 V step, R = 1 kΩ, C = 1000 µF, so τ = 1 s. V_C(1 s) = 12(1 − e⁻¹) = **7.5854 V**, and V_C(5 s) = **11.919 V**.
- **AC RC low-pass:** R = 1 kΩ, C = 100 nF. fc = 1/(2πRC) = **1591.55 Hz**. At fc the gain is **−3.010 dB** and the phase is **−45.0°**.
- **DC sweep:** sweep BAT1 in REF-1 Scenario A from 9.0 to 13.0 V. Above 9 V, the hand-calc load current is I(V) = (V − 9)/15.1 using the fixed-drop model.

### 9.6 Tolerances
- **Engine (formula/MNA, fixed-drop LED model) vs hand calc:** relative error ≤ 1e-6.
- **ngspice vs hand calc:**
  - linear-only quantities (RC, sources and resistors, Scenario B): ≤ 0.5%;
  - quantities involving the LED diode model (Scenarios A and C): ≤ 3%, because ngspice uses the fitted exponential LED model instead of the fixed drop;
  - the transient time step must be ≤ τ/1000.

---

## 10. Milestones (student building with AI; evenings and weekends)

The estimates are realistic for about 10–15 h a week with Cursor / Grok Build. Each milestone is a set of PRs and ends usable.

| # | Milestone | Content | Est. |
|---|---|---|---|
| M0 | Skeleton | Repo, solution layout, CI (build + tests), README/AGENTS.md, empty WPF shell in pt-BR | 1 week |
| M1 | Units + Engine | Unit-safe quantities, parsing, formula registry, MNA DC solver, energy calcs, REF-1 engine tests green | 2–3 weeks |
| M2 | Project + manual schematic | Project JSON save/open, component library, canvas, place/drag/wire, live OP + energy table | 3–4 weeks |
| M3 | ngspice | Netlist generator (golden files), P/Invoke wrapper, OP/DC/AC/TRAN, plots, Spice tests green | 3–4 weeks |
| M4 | Grok idea → schematic | Grok client, schemas, validator, auto-layout, F1 flow, offline mode | 3 weeks |
| M5 | Grok explain + proposals | Explain with result IDs, number check, change cards, accept → recalc, history/undo | 2 weeks |
| M6 | Polish v0 | Warnings UX, exports (nice-to-have), bug bash with Testino, REF-1 end to end from text | 2 weeks |

**Total ≈ 16–19 weeks (about 4–5 months).** The **smallest usable version is M1 + M2 (~6 weeks)**: manual schematic plus trustworthy energy calculations, with no AI and no ngspice yet.

---

## 11. Acceptance criteria

Each item is a test/checklist entry. Wording is kept from the draft.

### Core: units (CORE)
- [ ] **CORE-1** Parsing `"4,7 kΩ"`, `"4.7k"` (in a resistance context), `"100 nF"`, `"20 mA"` and `"1 MΩ"` gives the correct SI values (4700, 4700, 1e-7, 0.02, 1e6).
- [ ] **CORE-2** Adding quantities with different dimensions (V + A) throws a typed error. V/Ω gives A, and V×A gives W.
- [ ] **CORE-3** Formatting picks a sensible prefix and uses pt-BR decimals (0.0198675 A → "19,87 mA").
- [ ] **CORE-4** Saving a project and reopening it gives an identical model (round-trip test on REF-1).
- [ ] **CORE-5** Opening a file with an unknown or newer `schemaVersion` shows a pt-BR error and does not crash. An older version is migrated.
- [ ] **CORE-6** Every engine result carries `resultId`, value, unit, formula ID and input IDs.
- [ ] **CORE-7** Engine assemblies have no reference to WPF or System.Windows. A CI check fails the build otherwise.
- [ ] **CORE-8** Computations taking more than 200 ms run off the UI thread, show progress and can be cancelled within 1 s.
- [ ] **CORE-9** Autosave every N minutes (default 2) into `history/`, keeping the last 20 snapshots.
  - *Note (D7):* N ranges from 1 to 60 min.

### Circuits: model, editing, energy (CIR)
- [ ] **CIR-1** All 10 v0 component types can be placed, configured with unit-checked params and saved.
- [ ] **CIR-2** Dragging a component keeps its wires attached. Undo/redo covers place, move, wire, delete and value edit (at least 50 steps).
- [ ] **CIR-3** Editing a value triggers recalculation, and the table updates within 1 s for REF-1.
- [ ] **CIR-4** The validator detects a missing ground, floating nodes, a short across a voltage source, and an unconnected pin. Each problem gets a pt-BR message that highlights the component.
- [ ] **CIR-5** The engine DC (MNA) solver reproduces REF-1 Scenarios A, B and C (§9.2–9.4) within 1e-6 relative error, for every listed current, voltage and power.
- [ ] **CIR-6** The per-component table shows V across, I through and P (with sign convention: + = absorbing) for every component.
- [ ] **CIR-7** Power-rating check: resistor P under 50% of rating is OK, 50–100% is a warning ("atenção"), and over 100% is an error ("excede a potência"). In REF-1 A, R = 150 Ω at 0.25 W gives OK. Changing it to 15 Ω (forcing about 200 mA per branch, if each LED still conducts) gives an error.
  - *Note (D3):* for the 15 Ω case the test checks only the status "exceeds power rating" ("excede a potência").
- [ ] **CIR-8** Energy: Wh/day = P_source × hours/day. REF-1 A gives 14.3046 Wh/day.
- [ ] **CIR-9** Battery runtime = capacity × DoD / I_discharge. REF-1 A gives 17.617 h, and autonomy gives 2.936 days.
- [ ] **CIR-10** Charge current and recharge time: REF-1 B gives 1.176471 A and 2.975 h, and REF-1 C gives a battery charge current of 0.974026 A.
- [ ] **CIR-11** Power balance: for any solved DC circuit, the sum of all component powers is 0 within 1e-9 W. The UI shows this as a "balanço de potência ✓" indicator.

### Simulation (SIM)
- [ ] **SIM-1** The netlist generator emits a valid SPICE netlist for REF-1 that matches a committed golden file, with no time-dependent noise.
- [ ] **SIM-2** `ngspice.dll` loads through P/Invoke from `native/ngspice/x64`. If it is missing, the app shows a pt-BR error and the engine features keep working.
- [ ] **SIM-3** `.op` on REF-1 matches the hand calc within §9.6 tolerances (A and C ≤ 3%, B ≤ 0.5%).
- [ ] **SIM-4** `.tran` on the RC reference gives V_C(1 s) = 7.5854 V and V_C(5 s) = 11.919 V, both within 0.5%.
- [ ] **SIM-5** `.ac` on the RC low-pass gives fc = 1591.55 Hz within 0.5%, and −3.01 dB ± 0.05 dB with −45° ± 0.5° at fc. A Bode plot is displayed.
- [ ] **SIM-6** A `.dc` sweep of BAT1 from 9 to 13 V in REF-1 A gives a load current curve matching (V − 9)/15.1 within 3% for V ≥ 10 V. The curve is plotted.
  - *Note (D4):* between 9 V and 10 V the result must be monotonic and continuous, checked in M3 against ngspice; no exact value is defined in v0.
- [ ] **SIM-7** Simulations run in the background with progress and cancel. A cancelled run leaves the UI responsive and the project unchanged.
- [ ] **SIM-8** ngspice errors and warnings are captured and shown in pt-BR context ("Erro na simulação: …"), with the raw log available.
- [ ] **SIM-9** The LED model is fitted so that the ngspice diode at If = 20 mA gives Vf = 3.00 V ± 0.02 V (a unit test on the fitted model).

### Grok assistant (AI)
- [ ] **AI-1** The API key is read from Configurações (DPAPI-protected) or `XAI_API_KEY`. No key appears in the repo, logs or project files; a test scans them.
- [ ] **AI-2** Idea → proposal: the response is validated against `circuit-proposal.v0`. Valid proposals produce a schematic. Invalid ones are rejected in full, with listed reasons and one automatic retry.
- [ ] **AI-3** Proposals with unknown types, bad units, dangling pins, no ground or out-of-range values are rejected. Tested with 10 or more malformed fixture JSONs, without a live API.
- [ ] **AI-4** Auto-layout places every component of a valid proposal with no overlapping symbols. Wires connect the correct pins (tested on REF-1 and 3 fixture proposals).
- [ ] **AI-5** The explain request sends engine results with result IDs, and the system prompt forbids computing numbers (snapshot test of the request payload).
- [ ] **AI-6** Any number in Grok's reply that does not match an engine result within 0.5% is marked "não verificado" (tested with a mocked reply).
- [ ] **AI-7** A change proposal shows a diff card. Accepting it applies the change, records history (source: grok) and triggers an engine recalculation. Rejecting it leaves the project untouched.
- [ ] **AI-8** No key, no network, 401/402/429 or a timeout shows a pt-BR status in the Assistant panel. Design, calculation and simulation still work, and the idea flow offers template or manual start.
- [ ] **AI-9** End to end: the text "um painel solar de 18 V carregando uma bateria de 12 V que alimenta uma fita de LEDs com resistor" produces a schematic equivalent to REF-1, after the user edits only values, and passes CIR-5 / SIM-3 (manual test by Testino).

---
---

## Glossary

| Term | Meaning |
|---|---|
| LABZ | App name |
| Engine | `Labz.Core.Engine`: formula registry + solvers; the only source of numbers |
| MNA | Modified nodal analysis (engine DC solver) |
| OP / DC / AC / TRAN | ngspice analyses `.op`, `.dc`, `.ac`, `.tran` |
| REF-1 | Reference project: solar panel → 12 V battery → LED strip (§9) |
| DoD | Depth of discharge |
| Voc / Rs / Isc | Panel open-circuit voltage / series resistance / short-circuit current |
| EMF / Rint | Battery ideal source voltage / internal resistance |
| Vf / If | LED forward voltage / forward current |
| DPAPI | Windows Data Protection API (API key storage) |
| resultId | ID of an engine result, quoted by Grok (e.g. `R1.P`) |
| Golden file | Committed expected netlist output for SIM-1 |

---

## 12. Resolved decisions

Decided by Sérgio on 2026-10-18. Body sections are updated to match.

| # | Topic | Decision | Applied in |
|---|---|---|---|
| D1 | Unit prefixes | The parser accepts prefixes up to T (tera). | §2 Units |
| D2 | File names and paths | SPEC.md and AGENTS.md live at the repo root; there is no CURSOR.md and no docs/spec-v0-core-circuits.md. Local repo: `C:\Users\Serginho\LABZ`; `EstiveJobson/labz` will be pushed to GitHub later. | §8 |
| D3 | CIR-7 (15 Ω) | The test checks only the status "exceeds power rating" ("excede a potência"). | §11 CIR-7 note |
| D4 | DC sweep 9–10 V | The result must be monotonic and continuous, checked in M3 against ngspice; no exact value is defined in v0. | §11 SIM-6 note |
| D5 | Grok boundary | Grok may propose components and values, but every proposal is marked "not verified" ("não verificado") until the engine recalculates. Grok never shows a result it calculated itself. | §1, §7.0a |
| D6 | Retry | 1 automatic retry, fixed in v0. | §7.4 |
| D7 | Autosave interval | 1 to 60 min (default stays 2 min). | §4 Configurações, CORE-9 note |
| D8 | Charge efficiency | Fixed at 1.0 in v0, not in the project JSON; becomes a parameter in a future version. | §2 Engine, §6.1, §9.1 |
| D9 | Peak-sun hours | Stored as a `usageProfiles` entry whose `target` equals the panel's id (e.g. `PV1`). | §6.1, §9.1 |
| D10 | Switches (engine) | Ideal in the engine; Ron and Roff exist only in the SPICE path (M3). | §2 Engine, §6.2 |
| D11 | Diode / LED model | Diode: Shockley equation solved with Newton's method. LED: fixed forward-voltage drop. | §2 Engine |

### 12.1 Open questions
1. Whether the next module after circuits should be thermal (a natural follow-on for heat in components) or mechanics.
