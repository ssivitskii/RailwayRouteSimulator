# Railway Dynamics Lab UI

Angular 22 interface for the Railway Route Simulator. The UI edits route configuration and visualizes the trace returned by the .NET API; it does not implement train physics.

Start the API from the repository root:

```bash
ASPNETCORE_URLS=http://localhost:8080 dotnet run --project src/RailwaySimulator.Api
```

Then start the UI:

```bash
cd frontend
npm install
npm start
```

The development server opens on `http://localhost:4200` and proxies `/api` and `/health` to port 8080.

```bash
npm test
npm run build
npm run format:check
```

See the [root README](../README.md) for the API contract, safety limits, architecture, and CLI commands.
