---
name: reflow-data-processing
description: "Guidelines and architecture for Reflow's Data Processing plane (DuckDB, Tabular Frames, Codecs, Artifacts)."
---

# Reflow Data Processing Plane

## Core Mission

The `DataProcessing` module provides reusable analytical execution, data interchange, codecs, and artifact storage mechanisms for Reflow. It acts as the analytical **Data Plane**, cleanly separated from the PostgreSQL **Control Plane**.

---

## 1. Key Components

### 1. Tabular Frames (`Abstractions/Frame.cs`)
- `Frame` is the canonical tabular in-memory representation exchanged between nodes.
- Owns column headers (`Columns`) and string-cell row lists (`Rows`).
- Enforces row caps (`MaxRows = 50,000`) and character caps (`MaxStoredChars = 500,000`) to guarantee memory safety during pipeline runs.
- Provides ergonomic methods: `Empty`, `RowCount`, `ColumnCount`, `ColumnIndex(name)`, `HasColumn(name)`.

### 2. Codecs (`InMemory/CsvCodec.cs` & `InMemory/JsonCodec.cs`)
- **CSV Codec (`CsvCodec`):**
  - Robust quote-aware tokenizer (`""` escaping, embedded commas, CRLF / LF line breaks).
  - Configurable delimiters (comma, semicolon, tab, pipe).
  - Automatic column header detection or dynamic fallback (`col1`, `col2`, ...).
  - Duplicate column name validation.
- **JSON Codec (`JsonCodec`):**
  - Parses JSON documents and navigates nested property/array paths (e.g. `data.items[0]`).
  - Converts scalar elements into tabular cells while preserving nested JSON objects/arrays.
- **Frame Writer (`FrameWriter`):**
  - Serializes `Frame` to standard JSON arrays or formatted CSV strings with RFC-compliant quote escaping.

### 3. DuckDB Analytical Engine (`DuckDb/DuckDbSqlEngine.cs`)
- Analytical SQL queries (joins, aggregations, window functions, filtering, sorting) execute via DuckDB.
- **Sandbox Security:** Every query executes on an isolated in-memory DuckDB connection with `SET enable_external_access=false;` to prohibit arbitrary filesystem access or network calls.
- **Table Sanitization:** Source node names are sanitized into valid SQL identifiers (alphanumeric and underscores), preventing SQL injection on table definitions.
- **Parameterized Ingestion:** Upstream tabular frames are loaded into DuckDB using parameterized batch inserts.

### 4. Run Artifacts (`Artifacts/LocalArtifactStore.cs`)
- Persists node output tables as downloadable artifacts (CSV or JSON).
- Sanitizes file paths and node identifiers to prevent directory traversal (`../`) attacks.
- Computes SHA-256 checksums, byte sizes, and row counts (`ArtifactInfo`).
- Exposes downloadable streams via `GET /runs/{id}/tasks/{nodeId}/artifact`.

---

## 2. Extension Points for Future Slices

When extending the Data Plane:
1. **Parquet Storage:** Add a Parquet writer using DuckDB's native `COPY ... TO 'file.parquet'` command or Arrow C# bindings.
2. **Columnar Arrow Interchange:** Swap row-oriented `List<List<string?>>` with Apache Arrow `RecordBatch` for zero-copy memory buffers between DuckDB nodes.
3. **External Connectors:** Introduce reader abstractions for S3/Blob storage without exposing credentials to node definitions.
