# Kallipr Telemetry

A simple API for ingesting and querying device telemetry, using .NET 8 and SQLite.

## What you need to install

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Node.js 18+](https://nodejs.org/) (only needed for the UI)

## Running the API in Powershell

dotnet run --project task-1-backend\Kallipr.Telemetry.Api\Kallipr.Telemetry.Api.csproj

The API runs at `http://localhost:5010`. The SQLite database file (`telemetry.db`) is created automatically.

## Running the UI in Powershell

npm --prefix task-3-ui install
npm --prefix task-3-ui run dev

The UI runs at `http://localhost:5173`.

## Running the tests in Powershell

dotnet test

## Checking the API is healthy

```
GET http://localhost:5010/health
```

Should return `Healthy`.

## Sending a telemetry reading

```
POST http://localhost:5010/api/telemetry
Content-Type: application/json

{
  "tenantId": "acme",
  "deviceId": "dev-123",
  "type": "water_level",
  "value": 1.23,
  "unit": "m",
  "battery": 62,
  "signal": -85,
  "recordedAt": "2025-01-10T10:15:00Z",
  "externalId": "r-789"
}
```

## Querying readings

```
GET http://localhost:5010/api/telemetry/{tenantId}
```

Optional filters: `deviceId`, `type`, `from`, `to`, `page`, `pageSize`.

The database is stored in `/data/telemetry.db` inside the container.

## CI

GitHub Actions pipeline at [.github/workflows/ci.yml](.github/workflows/ci.yml):
- Restores → Builds → Tests (with TRX results published)
- Builds Docker image on `main` pushes
