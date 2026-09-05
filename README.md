# Railway Route Simulator

A deterministic .NET command-line simulator for train movement across powered tracks, unpowered tracks, and stations. It preserves a small physics-focused domain model while accepting practical JSON route definitions.

## Features

- Simulates acceleration, braking, traversal time, and station stops.
- Enforces train force, station entry-speed, and route final-speed limits.
- Validates finite, physically meaningful configuration values.
- Produces deterministic text, JSON, or CSV output and distinct exit codes.
- Analyzes a shared execution trace with elapsed/moving time, configured and executed station wait, planned distance, sampled speeds, modeled acceleration, evaluated safety margins, and an explicitly labeled tightest-constraint heuristic.
- Compares two or more route files with a documented stable ranking policy.
- Optimizes the configured initial speed over a bounded deterministic grid, rebuilding the scenario for every candidate.

## Tech Stack

C# · .NET 9 · System.Text.Json · xUnit

## Architecture

`RailwaySimulator.Domain` contains the physics model, value objects, route sections, explicit result types, and section-by-section execution trace. `RailwaySimulator.Application` maps external configuration into fresh domain scenarios and derives analysis, comparison, and optimization reports from that trace. `RailwaySimulator.Cli` owns file I/O, argument validation, and deterministic output formatting.

## Project Structure

- `src/RailwaySimulator.Domain` — train, route, sections, value objects, results.
- `src/RailwaySimulator.Application` — JSON configuration mapping and simulation reports.
- `src/RailwaySimulator.Cli` — executable entry point.
- `tests/RailwaySimulator.Tests` — domain, boundary, configuration, and CLI tests.
- `examples` — ready-to-run configurations.

## Getting Started

Requires the .NET 9 SDK.

## Build

```bash
dotnet build RailwayRouteSimulator.slnx -c Release
```

## Run

```bash
dotnet run --project src/RailwaySimulator.Cli -- simulate examples/simple-route.json
```

Detailed analysis:

```bash
dotnet run --project src/RailwaySimulator.Cli -- analyze examples/stations-route.json --format json
```

Compare routes:

```bash
dotnet run --project src/RailwaySimulator.Cli -- compare examples/simple-route.json examples/stations-route.json --format csv
```

Optimize initial speed using an inclusive 41-point grid:

```bash
dotnet run --project src/RailwaySimulator.Cli -- optimize examples/simple-route.json --min 0 --max 5 --iterations 41
```

Every command accepts `--format text|json|csv`; `--json` remains an alias for `--format json`. Exit codes are `0` when the reported or recommended route succeeds, `1` when it fails, `2` for usage/configuration errors, and `3` for file-system errors.

## Tests

```bash
dotnet test RailwayRouteSimulator.slnx -c Release
```

## Examples

- `simple-route.json` — powered acceleration followed by normal track.
- `stations-route.json` — traversal with a station stop.
- `invalid-speed-route.json` — intentionally violates the final speed limit.

## Design Decisions

The physics model stays synchronous because it is CPU-only and deterministic. JSON and console I/O are isolated at the boundary. Explicit result records describe expected simulation failures without using exceptions for control flow.

Analysis reports only values supported by the model. Configured station wait is a route-plan value, while executed station wait and moving time are derived from successful execution. Planned track distance is always identified as planned; actual distance is reported only for a successful route because failed traversal does not expose partial distance. A failed section likewise reports unknown elapsed time, exit speed, and acceleration rather than zero or stale measurements. Minimum, average, and maximum speeds are calculated from the initial speed and successful section-exit samples rather than claimed as continuous extrema. Modeled acceleration comes from the force/mass calculation actually used by powered sections and station maneuvers. The reported tightest constraint is a diagnostic heuristic, not a claim about globally optimal physical throughput.

Comparison ranks successful routes above failed routes. Within each result group, all routes no more than 1% slower than that group's globally fastest route form one safety cohort ordered by larger evaluated station/final speed-limit margin. Routes outside the cohort are ordered by elapsed time, and remaining ties preserve input order. This produces a stable total order without pairwise tolerance cycles. Optimization varies only `initialSpeed`, evaluates an inclusive grid of 2–1001 points, creates a fresh scenario per point, and applies the same ranking policy to successful candidates. If every candidate fails, the report contains no recommendation.

## Limitations / Future Improvements

The model uses fixed time steps and intentionally omits track gradients, drag, and multi-train scheduling. Those features should only be introduced together with clear physical assumptions and regression scenarios.
