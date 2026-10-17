# Usage Guide

## Requirements

- .NET 10 SDK
- PostgreSQL 16+
- Node 22+

## Running Reflow

### 1. Apply the control-plane schemas

Each module owns its own schema, so apply each migration context once:

```bash
dotnet ef database update --context IdentityDbContext          --project src/Reflow.Api
dotnet ef database update --context PipelinesDbContext         --project src/Reflow.Api
dotnet ef database update --context PipelineExecutionDbContext --project src/Reflow.Api
dotnet ef database update --context TriggersDbContext          --project src/Reflow.Api
```

### 2. Start the API

```bash
dotnet run --project src/Reflow.Api --urls http://localhost:5001
```

`.\dev.ps1` starts the backend and frontend together.

### 3. Start the editor

```bash
cd frontend && npm install && npm run dev   # http://localhost:5173
```

## Triggers

Triggers start pipeline runs. They never execute a pipeline inline, and a run always
executes a previously published, immutable version.

### Manual

```text
POST /api/v1/pipelines/{id}/runs        start a run → 202 { id }
```

### Schedule

```text
POST /api/v1/pipelines/{id}/triggers/schedules   { name, cron, timezone, overlap }
```

- 5-field cron expressions with a timezone.
- An overlap policy of `skip` or `queue`.
- A background scheduler worker that enforces queue caps.

### Webhook

```text
POST /api/v1/pipelines/{id}/triggers/webhooks    create or rotate → { token } (shown once)
POST /api/v1/hooks/{token}                       inbound JSON → 202 { id }
```

- The webhook URL embeds a secret token (stored as a SHA-256 hash, regenerable).
- The request body becomes the run payload: a 600 KB cap, and it must be JSON.
- Idempotency: send `X-Event-Id` (or an `eventId` field in the body) and replays return the original run instead of starting a duplicate.

Inside a pipeline, the `trigger.payload` node (with an optional `rootPath`) parses
the payload so downstream SQL can query it like any other table.

## Testing

```bash
dotnet test        # unit + API integration (requires PostgreSQL; creates reflow_test)
cd frontend && npm run build
```
