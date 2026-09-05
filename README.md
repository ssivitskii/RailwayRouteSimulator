# Railway Route Simulator

A deterministic .NET simulator for train movement across powered tracks, unpowered tracks, and stations, available through a CLI and an interactive Angular engineering console. It preserves a small physics-focused domain model while accepting practical JSON route definitions.

## Features

- Simulates acceleration, braking, traversal time, and station stops.
- Enforces train force, station entry-speed, and route final-speed limits.
- Validates finite, physically meaningful configuration values.
- Produces deterministic text, JSON, or CSV output and distinct exit codes.
- Analyzes a shared execution trace with elapsed/moving time, configured and executed station wait, planned distance, sampled speeds, modeled acceleration, evaluated safety margins, and an explicitly labeled tightest-constraint heuristic.
- Compares two or more route files with a documented stable ranking policy.
- Optimizes the configured initial speed over a bounded deterministic grid, rebuilding the scenario for every candidate.
- Exposes route analysis through a small ASP.NET Core API without duplicating physics in the browser.
- Provides an editable route builder, trace playback, speed profile, metrics, and accessible section table.

## Tech Stack

C# · .NET 9 · ASP.NET Core · System.Text.Json · xUnit · Angular 22 · TypeScript · Vitest

## Architecture

`RailwaySimulator.Domain` contains the physics model, value objects, route sections, explicit result types, and section-by-section execution trace. `RailwaySimulator.Application` maps external configuration into fresh domain scenarios and derives analysis, comparison, and optimization reports from that trace. `RailwaySimulator.Cli` owns file I/O, argument validation, and deterministic output formatting. `RailwaySimulator.Api` maps HTTP JSON directly through the same application service. `frontend` renders only API results: its animation and SVG interpolation are presentation concerns, never an alternative physics engine.

## Project Structure

- `src/RailwaySimulator.Domain` — train, route, sections, value objects, results.
- `src/RailwaySimulator.Application` — JSON configuration mapping and simulation reports.
- `src/RailwaySimulator.Cli` — executable entry point.
- `src/RailwaySimulator.Api` — HTTP analysis endpoint, health check, and development Swagger UI.
- `tests/RailwaySimulator.Tests` — domain, boundary, configuration, and CLI tests.
- `tests/RailwaySimulator.ApiTests` — in-memory HTTP contract tests.
- `frontend` — standalone strict Angular application and unit tests.
- `examples` — ready-to-run configurations.

## Getting Started

Requires the .NET 9 SDK. The interactive UI additionally requires a current Node.js release supported by Angular 22 and npm.

## Interactive UI

Run the API and UI in two terminals from the repository root:

```bash
ASPNETCORE_URLS=http://localhost:8080 dotnet run --project src/RailwaySimulator.Api
```

```bash
cd frontend
npm install
npm start
```

Open `http://localhost:4200`. The Angular development server proxies `/api` and `/health` to `http://localhost:8080`, so the API does not need a broad CORS policy.

The screen includes two repository-example presets, train inputs, a dynamic powered/normal/station section editor, API validation feedback, deterministic trace playback with station holds, an SVG speed profile, KPI cards, and a horizontally scrollable trace table.

## HTTP API

`POST /api/simulations/analyze` accepts the same JSON shape used by files in `examples` and returns a camel-case `SimulationAnalysis` with `report`, `metrics`, and `trace`. It never accepts a server-side file path or launches the CLI.

`GET /health/live` is a process liveness endpoint. In development, Swagger UI is available at `/swagger`.

The HTTP boundary limits request bodies to 128 KiB, routes to 64 sections, and fixed-step precision to at least `0.001` seconds. The domain continues to validate physical values and rejects non-finite intermediate arithmetic. Each train has one shared one-million-step integration budget across every section and station maneuver, so route length cannot multiply the CPU ceiling. The analyze endpoint permits four concurrent simulations with no queue and returns HTTP 429 when capacity is occupied; health and Swagger endpoints are not rate-limited. Error responses use `application/problem+json`; unexpected exception details are not exposed.

## Build

```bash
dotnet build RailwayRouteSimulator.slnx -c Release
npm --prefix frontend run build
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
npm --prefix frontend test
npm --prefix frontend run format:check
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

The model uses fixed time steps and intentionally omits track gradients, drag, and multi-train scheduling. Those features should only be introduced together with clear physical assumptions and regression scenarios. A simulation is synchronously CPU-bound and does not currently accept mid-run cancellation; the shared work budget and endpoint concurrency limiter provide deterministic resource ceilings instead.
