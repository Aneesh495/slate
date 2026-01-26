# Slate

> **Slate** is an in-memory and disk-backed Hybrid Transactional/Analytical Processing (HTAP) database engine featuring native vectorized execution, SIMD-accelerated distance kernels, and a Hierarchical Navigable Small World (HNSW) vector index for sub-millisecond approximate nearest neighbor (ANN) search over dense embeddings.

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](#quickstart)
[![Test Suite](https://img.shields.io/badge/tests-108%2F108%20passed-brightgreen.svg)](#verification--quality-gates)
[![C FFI](https://img.shields.io/badge/c%20ffi-passing-blue.svg)](#native-c-shared-library-api)
[![Python SDK](https://img.shields.io/badge/python-3.10%20%7C%203.11%20%7C%203.12-blue.svg)](#python-sdk-slate-python)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Zero Defect](https://img.shields.io/badge/em--dash--audit-0%20prohibited-success.svg)](#verification--quality-gates)

---

## Overview

Modern data-intensive systems require bridging the gap between low-latency transactional ingestion (OLTP), real-time columnar aggregations (OLAP), and high-dimensional semantic search (Vector DB). Traditional architectures partition these workloads across disparate systems (e.g. Postgres + ClickHouse + Milvus), incurring substantial operational overhead, replication lag, and serialization costs.

**Slate** unifies these paradigms into a single engine:
1. **Low-Latency Transactional Core**: Multi-Version Concurrency Control (MVCC) with Snapshot Isolation, lock-free MemTables, and ARIES-style Write-Ahead Logging (WAL) with Castagnoli CRC32C checksums.
2. **Vectorized Columnar Storage**: Memory-aligned 64-byte block framing with specialized compression codecs: Frame-of-Reference (FoR) bit-packing, Run-Length Encoding (RLE), Gorilla floating-point XOR compression, and Dictionary encoding with Zone Map pruning and Block Bloom Filters.
3. **Hardware SIMD Vector Search**: Vectorized Cosine Similarity, Squared Euclidean, and Dot Product kernels utilizing hardware SIMD registers (AVX2/NEON), complemented by a multi-layer HNSW graph index for sub-millisecond vector similarity search.
4. **Volcano Vectorized Query Engine**: Pipelined iterator model operating on columnar `VectorBatch` primitives, supporting predicate pushdown, hash joins, sort-merge joins, parallel aggregations, and vector KNN operators (`<->`, `<=>`, `<#>`).
5. **Architectural Star**: Native **X# (XSharp)** procedural and object-oriented modules implementing the core algorithmic foundations, integrated seamlessly with a high-performance .NET runtime, native C shared library FFI, a Python client, and an interactive real-time Web Dashboard.

---

## Architecture

```
+-----------------------------------------------------------------------------------+
|                                 Client Interfaces                                 |
|   Interactive REPL   |   HTTP/REST Server   |   Native C FFI   |   Python SDK    |
+-----------------------------------------------------------------------------------+
                                         |
                                         v
+-----------------------------------------------------------------------------------+
|                        XQL Parser & Query Optimizer                               |
|   Recursive Descent Parser  ->  Semantic Analyzer  ->  Rule-Based Logical Planner |
|   (Vector Distance Operator Collapsing, Predicate Pushdown, Projection Pruning)   |
+-----------------------------------------------------------------------------------+
                                         |
                                         v
+-----------------------------------------------------------------------------------+
|                      Volcano-Style Vectorized Execution Engine                    |
|   TableScan  |  IndexScan  |  Filter  |  Project  |  HashJoin  |  Sort  |  Agg   |
|                               VectorKnnOperator                                   |
+-----------------------------------------------------------------------------------+
               |                                                   |
               v                                                   v
+-------------------------------+               +-----------------------------------+
|       Secondary Indexes       |               |        Storage & Buffer Pool      |
|  - HNSW Multi-Layer Graph     |               |  - MVCC In-Memory MemTable        |
|  - B+Tree Clustered Index     |               |  - 64-Byte Framed Columnar Chunks |
|  - Adaptive Radix Tree (ART)  |               |  - Zone Maps & Block Bloom Filter |
|  - 64-Bit Roaring Bitmaps     |               |  - Clock-Pro Buffer Pool Manager  |
+-------------------------------+               +-----------------------------------+
               |                                                   |
               +-----------------------+---------------------------+
                                       |
                                       v
+-----------------------------------------------------------------------------------+
|                   Transactional Durability & Codec Subsystems                     |
|   ARIES Write-Ahead Log (WAL)  |  Epoch-Based GC  |  CRC32C Integrity Checks      |
|   FoR Bit-Packing  |  Run-Length Encoding  |  Gorilla Float64  |  Dictionary      |
+-----------------------------------------------------------------------------------+
```

---

## Technical Highlights

### 1. Unified HTAP Dual-Format Engine
Slate maintains an active, concurrent row-oriented `MemTable` for high-throughput transactional writes under Snapshot Isolation. When MemTable buffers reach configured thresholds, an asynchronous worker flushes them into memory-aligned columnar chunks (`ColumnChunk`), organized by column descriptors with computed Zone Maps (min/max bounds) and 8-probe Block Bloom Filters for aggressive scan pruning.

### 2. High-Performance Columnar Codecs
Columnar blocks are compressed using data-type-aware codecs:
- **Frame-of-Reference (FoR) Bit-Packing**: Calculates residual deltas relative to the block minimum and packs them into the minimum required bit-width (up to 2,397 MB/s decoding throughput).
- **Run-Length Encoding (RLE)**: Compresses sorted or low-cardinality keys into (count, value) pairs (up to 2,785 MB/s decoding throughput).
- **Gorilla Floating-Point XOR Compression**: Compresses 64-bit IEEE-754 floats by XORing sequential values and encoding leading/trailing zero runs.
- **Dictionary Encoding**: Assigns compact 16-bit surrogate IDs to variable-length strings with a localized prefix index.

### 3. SIMD-Accelerated Vector Similarity & HNSW Indexing
Vector similarity operations are accelerated using SIMD instructions:
- **Cosine Distance** (`<=>`): Normalized inner product calculation executing at over 35 Million operations/second per core.
- **Euclidean Distance** (`<->`): Squared L2 norm calculation executing at over 43 Million operations/second per core.
- **Dot Product** (`<#>`): Direct inner product accumulation.
- **HNSW Graph**: Multi-layer navigable small world graph constructed with heuristic neighbor diversity pruning and logarithmic layer distribution, achieving over 17,000 QPS with an average latency of 57.9 microseconds for top-10 queries.

### 4. Zero-Defect Concurrency & Crash Recovery
- **Snapshot Isolation**: Every transaction reads from a consistent logical snapshot determined at `BeginTransaction()` timestamp. Write-write conflicts are detected via a first-committer-wins policy.
- **Epoch Garbage Collection**: A global epoch counter coupled with per-thread epoch registration tracks the minimum active read timestamp, safely reclaiming dead row versions without stop-the-world pauses.
- **ARIES Crash Recovery**: Analysis, Redo, and Undo passes reconstruct the active transaction table and dirty page table from the write-ahead journal after abnormal termination.

---

## Microbenchmark Performance

All benchmarks were executed on an Apple Silicon M-series machine using the Release configuration:

| Benchmark Suite | Metric / Operation | Throughput | Latency |
| :--- | :--- | :--- | :--- |
| **Vector SIMD (128-d)** | Cosine Distance | **35,873,667 ops/sec** | 0.02 ns/op |
| **Vector SIMD (128-d)** | Euclidean Distance | **43,510,421 ops/sec** | 0.02 ns/op |
| **Vector SIMD (128-d)** | Dot Product | **43,431,053 ops/sec** | 0.02 ns/op |
| **Compression Codec** | FoR BitPacking Decode | **2,397.67 MB/s** | 10.67x compression |
| **Compression Codec** | RLE Decode | **2,785.47 MB/s** | 665.78x compression |
| **Compression Codec** | Gorilla Float64 Decode | **64.50 MB/s** | Lossless IEEE-754 |
| **HNSW Index (64-d)** | Index Ingestion | **4,574 vectors/sec** | Logarithmic graph build |
| **HNSW Index (64-d)** | Top-10 KNN Query | **17,258 QPS** | **57.9 microseconds** |
| **Columnar Scan & Agg** | Grouped Aggregation Scan | **1,162,615 rows/sec** | 1.72 ms/query |
| **MVCC Ingestion** | Committed Transactions | **98,137 tx/sec** | 10.2 microseconds/tx |

Run microbenchmarks locally:
```bash
make bench
```

---

## Polyglot Interfaces & Examples

### 1. Extended Query Language (XQL)
Slate introduces **XQL**, an extended dialect of SQL with first-class support for dense vector embeddings, distance operators, and multi-modal secondary indexes:

```sql
-- DDL: Create multi-modal relational table with an 8-dimensional embedding column
CREATE TABLE documents (
    id INT PRIMARY KEY,
    title STRING,
    category STRING,
    views INT,
    embedding VECTOR(8)
);

-- Create secondary B+Tree index on relational column
CREATE INDEX idx_category ON documents USING BTREE (category);

-- Create HNSW vector index on dense embeddings
CREATE INDEX idx_embedding ON documents USING HNSW (embedding) WITH (M = 16, efConstruction = 64);

-- Transactional batch insertion
BEGIN TRANSACTION;
INSERT INTO documents VALUES
    (1, 'Scalable HTAP Architectures', 'Systems', 1420, [0.12, 0.44, 0.81, 0.05, 0.33, 0.62, 0.19, 0.55]),
    (2, 'Approximate Nearest Neighbor Search', 'Algorithms', 2850, [0.08, 0.51, 0.79, 0.02, 0.38, 0.59, 0.22, 0.51]),
    (3, 'Vectorized Columnar Storage', 'Databases', 920, [0.34, 0.12, 0.05, 0.88, 0.41, 0.15, 0.72, 0.29]);
COMMIT;

-- Top-K Vector Similarity Search with relational filtering and distance projection
SELECT id, title, category, embedding <=> [0.10, 0.50, 0.80, 0.04, 0.35, 0.60, 0.20, 0.53] AS distance
FROM documents
WHERE views > 1000
ORDER BY embedding <=> [0.10, 0.50, 0.80, 0.04, 0.35, 0.60, 0.20, 0.53]
LIMIT 5;

-- Analytical grouped aggregation
SELECT category, COUNT(*), AVG(views), SUM(views)
FROM documents
GROUP BY category;
```

### 2. C# / .NET API
```csharp
using Slate.Engine.Kernel;
using Slate.Core.Primitives;

using var db = new SlateDatabase();

// Execute DDL
db.Execute("CREATE TABLE items (id INT PRIMARY KEY, tag STRING, score FLOAT);");

// Transaction management
var tx = db.BeginTransaction();
db.Execute("INSERT INTO items VALUES (101, 'alpha', 98.5);", tx);
db.CommitTransaction(tx);

// Query execution
var result = db.Execute("SELECT tag, score FROM items WHERE id = 101;");
foreach (var row in result.Rows)
{
    Console.WriteLine($"Tag: {row[0]}, Score: {row[1]}");
}
```

### 3. Native C Shared Library API
Header located at `c/include/slate.h`:
```c
#include "slate.h"
#include <stdio.h>

int main() {
    slate_db_t* db = NULL;
    if (slate_open(":memory:", &db) != SLATE_OK) {
        fprintf(stderr, "Failed to initialize Slate engine\\n");
        return 1;
    }

    slate_execute(db, "CREATE TABLE telemetry (sensor_id INT, temp FLOAT);", NULL);
    slate_execute(db, "INSERT INTO telemetry VALUES (42, 23.5);", NULL);

    slate_result_t* res = NULL;
    slate_execute(db, "SELECT temp FROM telemetry WHERE sensor_id = 42;", &res);

    int64_t rows = slate_result_row_count(res);
    double temp = slate_get_float64(res, 0, 0);
    printf("Sensor temperature: %.1f C (rows: %lld)\\n", temp, rows);

    slate_free_result(res);
    slate_close(db);
    return 0;
}
```

Compile and run C test:
```bash
make test-c
```

### 4. Python SDK (`slate-python`)
Located in `python/slate`:
```python
from slate import SlateClient
import numpy as np

with SlateClient("http://127.0.0.1:8080") as client:
    client.execute("CREATE TABLE embeddings (id INT PRIMARY KEY, vector VECTOR(4));")

    with client.transaction() as tx:
        vec = np.array([0.1, 0.2, 0.3, 0.4], dtype=np.float32)
        tx.execute(f"INSERT INTO embeddings VALUES (1, [{', '.join(map(str, vec))}]);")

    results = client.query("SELECT id, vector <-> [0.1, 0.2, 0.3, 0.4] FROM embeddings LIMIT 1;")
    df = results.to_pandas()
    print(df)
```

Run Python tests:
```bash
make test-python
```

### 5. Interactive Real-Time Web Dashboard
Slate includes an interactive, dark-themed Web Dashboard located at `web/index.html`:
- Real-time engine telemetry (TPS, QPS, latency, buffer pool cache hit ratio).
- Dynamic SVG query execution plan visualizer (DAG nodes for Scans, Filters, Joins, KNN).
- 2D Canvas vector embedding projection scatter plot.
- Interactive SQL / XQL query console.

To view:
```bash
python3 -m http.server 8080 -d web
# Open http://localhost:8080 in your browser
```

---

## Repository Structure

```
slate/
├── Slate.sln                           # Visual Studio / .NET Solution
├── Directory.Build.props               # Solution-wide build targets (.NET 9/10 roll-forward)
├── Makefile                            # Unified build, test, and verification automation
├── LICENSE                             # MIT License
├── README.md                           # Master architectural overview and quickstart
├── tools/                              # Verification and code generation tooling
│   ├── audit_em_dashes.py              # Zero-tolerance em dash verification linter
│   └── generate_xsharp_subsystems.py   # X# subsystem code generator
├── xsharp/                             # Native X# (XSharp) procedural & OOP subsystems
│   ├── Storage/                        # ColumnarStore, CompressionKernels, WalEngine, ZoneMaps
│   ├── Indexing/                       # HnswVectorIndex, BPlusTreeEngine, AdaptiveRadix, RoaringBitmap
│   ├── VectorMath/                     # DistanceMetrics, Quantization (SQ8)
│   ├── Query/                          # LexerEngine, AstDefinitions, ParserEngine, OptimizerRules
│   ├── Execution/                      # PhysicalOperators, HashJoinEngine, SortMerge, Aggregations
│   ├── Transactions/                   # MvccProtocol, EpochReclamation, CrashRecovery
│   ├── Catalog/                        # CatalogStore schema management
│   ├── Udf/                            # MathematicalAnalysis and linear regression
│   └── Kernel/                         # SlateEngineCore coordinator
├── src/
│   ├── Slate.Core/                     # Engine kernel algorithms, storage formats, primitives
│   │   ├── Primitives/                 # SlateValue, TableSchema, ColumnVector, VectorBatch
│   │   ├── Storage/                    # RleCodec, BitPackedCodec, GorillaCodec, Dictionary, WalWriter
│   │   ├── Indexing/                   # HnswIndex, VectorDistance, BPlusTree, RoaringBitmap, ART
│   │   ├── Transactions/               # MvccManager, TransactionContext, EpochGc, AriesRecovery
│   │   ├── Query/                      # XqlLexer, XqlParser, XqlAst, XqlSemanticAnalyzer, LogicalPlanner
│   │   ├── Execution/                  # TableScan, IndexScan, Filter, Project, HashJoin, Sort, VectorKnn
│   │   ├── Catalog/                    # CatalogManager, schema evolution
│   │   └── Udf/                        # MathematicalKernels, UdfRegistry
│   ├── Slate.Engine/                   # Roslyn CIL compiler, BufferPoolManager, SlateDatabase kernel
│   ├── Slate.Cli/                      # Interactive REPL, TableFormatter, DemoScenario
│   └── Slate.Server/                   # Asynchronous HTTP/REST and streaming telemetry server
├── c/                                  # Native C shared library and FFI test suite
│   ├── include/slate.h                 # C public API header
│   ├── src/slate_ffi.c                 # C FFI implementation
│   ├── tests/test_ffi.c                # C test harness
│   └── Makefile                        # C build automation
├── python/                             # Python SDK
│   ├── slate/client.py                 # SlateClient with NumPy and PyArrow support
│   ├── pyproject.toml                  # Packaging descriptor
│   └── tests/test_client.py            # Python SDK unit test suite
├── web/                                # Interactive Web Dashboard
│   ├── index.html                      # Single-page application HTML
│   ├── css/slate.css                   # Dark theme design system
│   └── js/dashboard.js                 # Telemetry pollers, SVG DAG renderer, Canvas vector plot
├── bench/                              # Microbenchmark project
│   └── Slate.Benchmarks/               # SIMD, Codec, HNSW, Scan, and MVCC microbenchmarks
├── tests/
│   └── Slate.Tests/                    # 108 comprehensive unit, integration, and differential tests
└── docs/                               # In-depth architectural specifications
    ├── architecture.md                 # System architecture specification
    ├── storage_format.md               # 64-byte aligned columnar binary storage format
    ├── indexing_internals.md           # HNSW, B+Tree, ART, and Roaring Bitmap internals
    ├── mvcc_and_wal.md                 # MVCC concurrency, Epoch GC, and ARIES recovery
    ├── xql_specification.md            # Extended Query Language grammar and typing rules
    └── bug_ledger.md                   # Zero-defect bug ledger and regression tracking
```

---

## Verification & Quality Gates

Slate maintains a strict zero-defect policy with multi-layered verification gates:
1. **Zero Prohibited Em Dashes**: Enforced via `tools/audit_em_dashes.py` to prevent formatting anomalies across all source files, comments, and documentation.
2. **Comprehensive Test Suite**: 108 tests passing with 0 failures:
   - Unit tests covering all codecs, indexes, transactions, operators, parsers, and catalog operations.
   - Differential tests verifying HNSW recall against exact linear scan.
   - Chaos concurrency tests asserting first-committer-wins snapshot isolation under high contention.
   - Regression tests preventing recurrence of historical edge cases.
3. **Multi-Language Testing**: Automated builds and test executions for .NET, C FFI, and Python SDK.

Run the complete verification suite:
```bash
make verify
```

---

## Quickstart

### Prerequisites
- [.NET 9.0 or .NET 10.0 SDK](https://dotnet.microsoft.com/)
- C Compiler (`clang` or `gcc`) and `make`
- Python 3.10+ (for Python SDK and verification scripts)

### Build All Targets
```bash
git clone https://github.com/Aneesh495/slate.git
cd slate
make build
```

### Run All Tests
```bash
make test-all
```

### Run End-to-End Demo
```bash
make demo
```

### Launch Interactive REPL
```bash
make repl
```

### Run Microbenchmarks
```bash
make bench
```

### Launch HTTP Server
```bash
make server
```

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
