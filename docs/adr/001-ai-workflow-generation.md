# ADR 001: AI workflow drafts, not AI execution

## Status

Accepted.

## Context

Reflow users describe data work in prose ("read the orders CSV, keep EU rows, sum
by region") but must translate that into a DAG by hand. A language model can
lower that cost, but it introduces three risks this codebase takes seriously:

1. **Model output is untrusted input.** A generated graph is JSON produced by a
   remote service, and a pipeline node can compile SQL.
2. **Provider lock-in is expensive.** Free-tier limits change, and a portfolio
   deployment should not require a paid account.
3. **AI must not become an execution authority.** The existing rules state that
   AI may propose changes but is never the authorization or execution authority.

## Decision

### Draft-only contract

The assistant returns a **draft**: name, explanation, nodes, edges, warnings. It
does not create, update, publish, or run a pipeline. The client persists a draft
through the existing `PUT /api/v1/pipelines/{id}` write path, so ownership
checks, graph validation, and versioning stay in exactly one place.

Drafts are **stateless**. Nothing is stored server-side, so there is no draft
table, no migration, no stale draft lifecycle, and no second source of truth.

### Validation is the trust boundary

Model output passes through `WorkflowDraftParser` and then
`WorkflowDraftValidator`, which enforces:

- node ids are lowercase slugs (they become DuckDB table names in `data.sql`),
- ids are unique, the graph is acyclic, and edges reference real nodes,
- every `nodeType` exists in `NodeTypeRegistry`,
- every config passes **that node handler's own `ValidateConfig`**,
- the node budget is respected.

The catalog sent to the model is generated from the registry and each node's
`INodeConfigSchema`; it is never a hand-maintained document that can drift from
the validators.

### Bounded repair loop

Invalid output is sent back with the exact validation errors, at most
`Ai:MaxRepairs` times. After that the API returns `422` with a summary. The model
never receives an open-ended tool loop, and no partial workflow is applied.

### Provider abstraction

`ILanguageModel` has one operation: return JSON text for a prompt.
`OpenAiCompatibleLanguageModel` speaks the OpenAI chat-completions shape and
uses `response_format: { "type": "json_object" }`. Groq's free tier is the
default deployment target; Gemini's compatibility endpoint or a local Ollama
instance is a configuration change, not a code change.

`json_schema` structured outputs were rejected for v1 because provider support
is still uneven. Server-side validation plus repair is stronger anyway: it
checks Reflow's own node contracts, not just JSON shape.

### Data boundaries

The model receives the calling user's **file ids and names only**, never file
contents and never another user's files. Prompts are length-capped, the request
is delimited and labelled as data (prompt-injection mitigation), and provider
response bodies are not surfaced to clients or logs.

The endpoint requires authentication and has a tighter rate-limit policy (`ai`)
than general API traffic. AI is disabled by default (`Ai:Enabled=false`) and the
API key comes from configuration/secrets, never from source control.

## Consequences

- The assistant cannot be used to execute arbitrary user code; at worst it
  produces a draft that fails validation.
- A generated workflow is only as good as the prompt and the model; repairs cost
  additional provider calls, bounded by `MaxRepairs`.
- Prompt injection is mitigated, not eliminated: the model can still propose an
  unhelpful graph. Validation limits the blast radius to "invalid draft".
- Clients must implement a review step; there is no server-side draft history.
- Live operation needs one provider key. CI and integration tests use a scripted
  `FakeLanguageModel`, so no network or secret is required to test the feature.
