# Solution Design
Author: Keenan Collison

Title: This is my solution document for the Kallipr assessment.

## Structure

The project is split into three parts:

- **`task-1-backend`** — ASP.NET Core 8 API with SQLite for storage
- **`task-1-tests`** — unit and integration tests for the API
- **`task-3-ui`** — small React + TypeScript frontend

The backend is organised by feature. All the telemetry-related code (endpoints, service, request/response models) lives in `Features/Telemetry/`.

## How the API works

There are two endpoints:

- `POST /api/telemetry` — accepts a JSON payload and saves it to the database
- `GET /api/telemetry/{tenantId}` — returns a paged list of readings for a tenant, with optional filters

When a reading comes in, the endpoint first validates the request (e.g. battery must be between 0–100, required fields must be present). If validation passes, it checks for a duplicate `externalId` for that tenant, and if there isn't one, saves the reading and returns a `201 Created` response.

When querying, you can filter by `deviceId`, `type`, and a time range (`from`/`to`). Results come back newest first, with `page` and `pageSize` for pagination.

## Decisions

**Minimal APIs** — used instead of controllers because there are only two endpoints and it keeps things simpler. Less boilerplate, easier to follow.

**EF Core + SQLite** — EF Core handles migrations automatically at startup so there's no manual database setup needed. The `telemetry.db` file is created on first run.

**Duplicate prevention** — if the same `externalId` is sent twice for the same tenant, the API returns a `409 Conflict`. This is checked in code first (so the error message is clear), and there's also a unique index in the database as a backup in case two requests come in at exactly the same time.

**Battery status** — the `batteryLow` flag is calculated on the fly when you query readings, based on a configurable threshold (default: 20%). This means changing the threshold in config takes effect immediately without touching any stored data.

**Structured logging** — every log line includes `tenantId`, `deviceId`, and `type` as separate fields, not just embedded in a message string. This makes it much easier to filter logs in a real environment (e.g. "show me all errors for device X").

**Correlation ID** — if a client sends an `X-Correlation-Id` header, the API echoes it back in the response and includes it in logs. This makes it easier to trace a specific request through logs. If no ID is provided, the internal trace ID is used as a fallback.

**Tenant in the URL** — `GET /api/telemetry/{tenantId}` keeps tenant scoping explicit and easy to test. In a real production app this would come from a validated auth token instead.

## Trade-offs

- Used GitHub Actions instead of Azure Pipelines — easier to run without extra setup, but the same concepts apply to either
- No authentication — would add JWT validation in a real system so tenants can only see their own data
- No pagination total count — would require a second `COUNT` query on every request; left out to keep it simple
- Tests use an in-memory SQLite database so each test is completely isolated and doesn't leave files on disk
