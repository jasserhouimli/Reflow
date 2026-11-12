---
name: reflow-interview-prep
description: "Master technical interview questions, architecture defense, and system tradeoffs for Reflow."
---

# Reflow Technical Interview Preparation Guide

This guide prepares you to defend Reflow in technical interviews for software engineering internships.

---

## 1. The 30-Second Elevator Pitch

> "Reflow is an open-source visual data pipeline orchestrator built in .NET 10 and React 19. It allows users to visually design DAGs of data processing nodes, compile and validate them into immutable versions, and trigger them via schedules, webhooks, or on-demand.
>
> Architecturally, it separates the **Control Plane** (PostgreSQL + EF Core for state, versions, and atomic worker task claims) from the **Data Plane** (an in-memory DuckDB analytical engine for running fast SQL transformations over CSV and JSON data without leaking external file access). The backend is structured as a modular monolith using vertical slices."

---

## 2. Core Architectural Decisions & Tradeoffs

### Question 1: "Why did you build a Modular Monolith instead of Microservices?"
* **Answer:**
  * For an orchestration system of this scale, microservices introduce premature distributed systems complexity: network latency between node steps, distributed transactions, dual-write consistency problems, and complex local dev setups.
  * A **Modular Monolith** gives us strong domain boundaries (Identity, Pipelines, PipelineExecution, NodeTypes, DataProcessing, Triggers) where each module owns its data and EF Core migrations.
  * Cross-module communication only happens through explicit C# interfaces (`IPipelineSnapshotProvider`, `IPipelineRunStarter`, `IPipelineAccessChecker`), never through shared database queries. If a module needs to be extracted to a microservice later, the boundary is already clean.

### Question 2: "Why separate the Control Plane (PostgreSQL) and the Data Plane (DuckDB)?"
* **Answer:**
  * **PostgreSQL (Control Plane):** Excellent ACID transactional guarantees, row-level locking for atomic task claims, JSONB storage for immutable versioned DAG snapshots, and reliable audit logs.
  * **DuckDB (Data Plane):** High-performance columnar analytical query engine. DuckDB excels at vectorised aggregations, joins, filters, and projections over tabular data.
  * By isolating DuckDB in-memory per query with `SET enable_external_access=false;`, we achieve sub-millisecond data transformation while preventing arbitrary filesystem reads or network access by pipeline SQL nodes.

### Question 3: "How does the DAG execution engine work under the hood?"
* **Answer:**
  1. **Compilation & Snapshot:** When a pipeline is published, its DAG is validated for cycles, disconnected nodes, and single-trigger rules, then frozen into an immutable version snapshot.
  2. **Run Initialization:** Starting a run creates a `PipelineRun` and marks root nodes (nodes with 0 upstream dependencies) as `Ready`. Downstream nodes are marked `Pending`.
  3. **Atomic Task Claiming:** The background `ExecutionWorker` queries for `Ready` or due `RetryScheduled` tasks, claiming them atomically in PostgreSQL:
     ```sql
     UPDATE "pipeline_execution"."TaskRuns"
     SET "Status"=2, "StartedAt"=NOW()
     WHERE "Id"=@id AND ("Status"=1 OR ("Status"=5 AND "NotBefore"<=NOW()))
     ```
     This prevents race conditions when running multiple worker instances.
  4. **Dynamic Dependency Unblocking:** When a task finishes with `Completed`, downstream tasks evaluate `ExecutionGraph.IsReady()`. Once all incoming dependency edges are satisfied, the downstream task moves from `Pending` to `Ready`.
  5. **Worker Wakeup:** Workers sleep on an asynchronous channel pulse (`WorkerWakeup`), waking up instantly when a task completes rather than aggressive database polling.

### Question 4: "How do you handle worker crashes and transient node failures?"
* **Answer:**
  * **Crash Recovery:** When `ExecutionWorker` boots up, it runs `RecoverStuckTasksAsync`, identifying any tasks left in `Running` status without an active worker heartbeat, and marks them for retry or failure.
  * **Bounded Retries:** Slices define whether an error is transient or permanent. Transient failures use exponential backoff with a `NotBefore` window and maximum attempt counts.
  * **Cancellation Propagation:** When a user cancels a run, a `CancellationTokenSource` linked to that run is cancelled, terminating active DuckDB/HTTP execution immediately and marking pending tasks as `Cancelled`.

### Question 5: "How is NodeTypes designed? How do you add a new node type?"
* **Answer:**
  * NodeTypes strictly follows **SOLID principles**:
    1. **Open-Closed Principle (OCP):** Nodes are independent vertical slices implementing `INodeHandler`. The engine contains **no switch statements or type checking**; it resolves handlers dynamically from `NodeTypeRegistry`.
    2. **Liskov Substitution Principle (LSP) & Parameter Object:** Rather than fracturing node execution into multiple specialized interfaces (`IRunScopedHandler`, `ITriggerPayloadHandler`), execution uses a single unified `NodeExecutionContext`. Every node receives its inputs, config, run id, node id, trigger payloads, and cancellation token in a single context without throwing unsupported method exceptions.
    3. **Dependency Inversion (DIP):** Node handlers declare dependencies on abstractions (`ISqlEngine`, `IDataReader`, `IDataArtifactStore`), keeping analytical engines swappable and testable.
  * Adding a new node type requires zero modifications to the execution engine. Simply create a new class implementing `INodeHandler` and register it in DI.

---

## 3. Security Defense Points

1. **DuckDB Sandbox:** DuckDB connections run in-memory and execute `SET enable_external_access=false;` before any query. Users cannot execute `read_csv('/etc/passwd')` or export data to disk via DuckDB SQL commands.
2. **Path Traversal Defense:** Artifact downloads and storage sanitize node IDs and filenames using strict alphanumeric whitelisting and enforce storage within the designated run directory.
3. **SSRF Guard:** Webhook ingestion and external HTTP calls validate destination IPs against private, loopback, and cloud metadata IP ranges (e.g., 169.254.169.254).
4. **Auth & CSRF:** Uses JWT stored in HttpOnly, SameSite=Strict cookies coupled with custom anti-forgery headers (`X-CSRF-TOKEN`).

---

## 4. Key Performance Numbers & Code Quality

* **.NET 10** Minimal APIs with zero startup reflection overhead.
* **100% Honest Codebase:** No placeholder mock objects or disconnected dead code.
* **84 Unit Tests & 25 Integration Tests:**
  * Unit tests run in under **1 second**.
  * Integration tests use **Testcontainers** for real PostgreSQL database verification.
