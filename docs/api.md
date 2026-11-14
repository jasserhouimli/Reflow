# API Reference

All routes are versioned under `/api/v1`, except the node-type palette and the
inbound webhook receiver. Runs and tasks are created from a pipeline's published
version.

## Pipelines

| Method | Route | Notes |
|---|---|---|
| POST | `/api/v1/pipelines` | Create a pipeline. |
| PUT | `/api/v1/pipelines/{id}` | Save the graph (nodes and edges). |
| POST | `/api/v1/pipelines/{id}/validate` | Validate the DAG and node configuration. |
| POST | `/api/v1/pipelines/{id}/publish` | Freeze an immutable version. |
| POST | `/api/v1/pipelines/{id}/runs` | Start a run → `202 { id }`. |

## Runs and tasks

| Method | Route | Notes |
|---|---|---|
| GET | `/api/v1/runs/{id}` | Run status. |
| GET | `/api/v1/runs/{id}/tasks` | Task states for the run. |
| GET | `/api/v1/runs/{id}/logs` | Execution logs. |
| GET | `/api/v1/tasks/{id}` | Task detail, including output JSON. |
| POST | `/api/v1/tasks/{id}/retry` | Retry a failed task. |

## Triggers and node types

| Method | Route | Notes |
|---|---|---|
| POST | `/api/v1/pipelines/{id}/triggers/schedules` | Create a cron schedule. |
| POST | `/api/v1/pipelines/{id}/triggers/webhooks` | Create or rotate a webhook token (shown once). |
| POST | `/api/v1/hooks/{token}` | Inbound webhook → `202` (idempotent). |
| GET | `/api/node-types` | Editor palette definitions. |

## Files

Uploaded sources for `file.read`. Content is stored by the Pipelines module and
is never returned by the list endpoint.

| Method | Route | Notes |
|---|---|---|
| POST | `/api/v1/files` | Upload `{ fileName, content }` (CSV/JSON, 2 MB cap, sha256-deduplicated). |
| GET | `/api/v1/files` | List the caller's files (id, name, metadata). |
| DELETE | `/api/v1/files/{id}` | Delete one of the caller's files. |

## Assistant

| Method | Route | Notes |
|---|---|---|
| POST | `/api/v1/assistant/workflows/generate` | `{ prompt }` → validated workflow draft (no persistence). |

The assistant returns `{ name, explanation, nodes, edges, warnings, repairAttempts }`.
It never saves a pipeline: persist the draft through `PUT /api/v1/pipelines/{id}`.
Responses: `400` invalid prompt, `401` unauthenticated, `422` no valid draft after
repairs, `429` provider rate limit, `502/504` provider failure or timeout,
`503` AI not configured.

## Identity

| Method | Route | Notes |
|---|---|---|
| POST | `/api/v1/auth/register` | Create an account. |
| POST | `/api/v1/auth/login` | Obtain a JWT (issued over an HttpOnly cookie). |
| POST | `/api/v1/auth/refresh` | Refresh the access token. |
| POST | `/api/v1/auth/logout` | End the session. |
| GET | `/api/v1/auth/me` | Current authenticated user. |
