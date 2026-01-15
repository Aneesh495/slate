# Extended Query Language (XQL) Specification

## 1. Language Overview

The Extended Query Language (XQL) is Slate's native query language. XQL combines ANSI SQL-92 relational semantics with first-class multi-dimensional vector literals, vector distance operators, and ACID transaction control statements.

---

## 2. Vector Distance Operators

XQL introduces three SIMD-accelerated binary vector distance operators:

| Operator | Mathematical Metric | Formula | Range |
|---|---|---|---|
| `<->` | Euclidean (L2) Distance | $d(u, v) = \sqrt{\sum_{i=1}^D (u_i - v_i)^2}$ | $[0, +\infty)$ |
| `<#>` | Negative Inner Product | $d(u, v) = -\sum_{i=1}^D u_i \cdot v_i$ | $(-\infty, +\infty)$ |
| `<=>` | Cosine Distance | $d(u, v) = 1 - \frac{u \cdot v}{\|u\|_2 \cdot \|v\|_2}$ | $[0, 2]$ |

### Vector Literal Syntax
Dense vectors are expressed as bracket-enclosed, comma-separated floating-point literals:
```sql
[0.15, -0.42, 0.88, 0.05]
```

### Top-K Vector Search Query Example
```sql
SELECT id, title
FROM corpus
ORDER BY embedding <-> [0.15, -0.42, 0.88, 0.05]
LIMIT 10;
```

---

## 3. Grammar (EBNF)

```ebnf
Statement           ::= SelectStatement
                      | InsertStatement
                      | UpdateStatement
                      | DeleteStatement
                      | CreateTableStatement
                      | CreateIndexStatement
                      | DropTableStatement
                      | TransactionStatement
                      | ExplainStatement

SelectStatement     ::= "SELECT" ProjectionList
                        "FROM" TableName [ "AS" Alias ]
                        [ JoinClause* ]
                        [ "WHERE" Expression ]
                        [ "GROUP BY" ExpressionList ]
                        [ "ORDER BY" OrderByList ]
                        [ "LIMIT" IntegerLiteral ] ";"

JoinClause          ::= [ "INNER" | "LEFT" | "RIGHT" | "CROSS" ] "JOIN" TableName [ "AS" Alias ] "ON" Expression

CreateTableStatement::= "CREATE TABLE" Identifier "(" ColumnDefList [ "," TableConstraint ] ")" ";"

ColumnDef           ::= Identifier DataType [ "NOT NULL" ] [ "PRIMARY KEY" ]

CreateIndexStatement::= "CREATE INDEX" Identifier "ON" Identifier "USING" IndexType "(" IdentifierList ")" [ "WITH" "(" PropertyList ")" ] ";"

IndexType           ::= "HNSW" | "BTREE" | "ART" | "ROARING_BITMAP"

TransactionStatement::= "BEGIN" [ "TRANSACTION" ] ";"
                      | "COMMIT" ";"
                      | "ABORT" ";"

ExplainStatement    ::= "EXPLAIN" Statement
```

---

## 4. Supported Data Types

| Type Name | Physical Storage | Description |
|---|---|---|
| `INT` / `BIGINT` | 64-bit Signed Integer | Standard 8-byte two's complement integer |
| `FLOAT` / `DOUBLE` | 64-bit IEEE 754 Float | Double-precision floating point |
| `FLOAT32` | 32-bit IEEE 754 Float | Single-precision floating point |
| `BOOLEAN` | 1-bit boolean | `TRUE` or `FALSE` |
| `STRING` / `TEXT` | UTF-8 String | Variable-length byte string |
| `TIMESTAMP` | 64-bit Integer | Microseconds since Unix epoch |
| `VECTOR(D)` | `float[D]` (Contiguous) | Dense $D$-dimensional single-precision vector |
