import json
import urllib.request
import urllib.error
from typing import List, Dict, Any, Optional, Union

class SlateError(Exception):
    """Base exception for Slate database operations."""
    pass

class QueryResult:
    """Encapsulates the tabular and vector results of an executed XQL query."""
    def __init__(self, columns: List[str], rows: List[List[Any]], rows_affected: int, execution_time_ms: float, explain: Optional[str] = None):
        self.columns = columns
        self.rows = rows
        self.rows_affected = rows_affected
        self.execution_time_ms = execution_time_ms
        self.explain = explain

    def __len__(self) -> int:
        return len(self.rows)

    def __iter__(self):
        return iter(self.rows)

    def __repr__(self) -> str:
        return f"<QueryResult columns={self.columns} rows={len(self.rows)} time={self.execution_time_ms:.2f}ms>"

    def to_dict(self) -> Dict[str, List[Any]]:
        """Converts columnar output into a dictionary of column-to-values."""
        result: Dict[str, List[Any]] = {col: [] for col in self.columns}
        for row in self.rows:
            for idx, col in enumerate(self.columns):
                val = row[idx] if idx < len(row) else None
                result[col].append(val)
        return result

    def to_pandas(self):
        """Converts query result to a pandas.DataFrame if pandas is installed."""
        try:
            import pandas as pd
            return pd.DataFrame(self.rows, columns=self.columns)
        except ImportError:
            raise ImportError("pandas is required for to_pandas(). Install via: pip install pandas")

    def to_arrow(self):
        """Converts query result to a pyarrow.Table if pyarrow is installed."""
        try:
            import pyarrow as pa
            return pa.Table.from_pydict(self.to_dict())
        except ImportError:
            raise ImportError("pyarrow is required for to_arrow(). Install via: pip install pyarrow")

    def to_numpy_vectors(self, vector_column: str):
        """Extracts dense multi-dimensional embeddings into a contiguous 2D numpy array."""
        try:
            import numpy as np
        except ImportError:
            raise ImportError("numpy is required for to_numpy_vectors(). Install via: pip install numpy")

        if vector_column not in self.columns:
            raise ValueError(f"Column '{vector_column}' not found in query result.")

        col_idx = self.columns.index(vector_column)
        vectors = [row[col_idx] for row in self.rows if row[col_idx] is not None]
        return np.array(vectors, dtype=np.float32)


class Transaction:
    """Manages an active ACID transaction session on the Slate server."""
    def __init__(self, client: "SlateClient", tx_id: int, read_timestamp: int):
        self.client = client
        self.tx_id = tx_id
        self.read_timestamp = read_timestamp
        self.is_active = True

    def query(self, sql: str) -> QueryResult:
        if not self.is_active:
            raise SlateError(f"Transaction {self.tx_id} is no longer active.")
        return self.client.query(sql, tx_id=self.tx_id)

    def commit(self):
        if not self.is_active:
            return
        self.client._post("/v1/tx/commit", {"tx_id": self.tx_id})
        self.is_active = False

    def abort(self):
        if not self.is_active:
            return
        self.client._post("/v1/tx/abort", {"tx_id": self.tx_id})
        self.is_active = False

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        if exc_type is not None:
            self.abort()
        else:
            self.commit()


class SlateClient:
    """Official HTTP and IPC client for the Slate HTAP database engine."""
    def __init__(self, endpoint: str = "http://127.0.0.1:8080"):
        self.endpoint = endpoint.rstrip("/")

    def _post(self, path: str, payload: Dict[str, Any]) -> Dict[str, Any]:
        url = f"{self.endpoint}{path}"
        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                res_body = resp.read().decode("utf-8")
                return json.loads(res_body)
        except urllib.error.HTTPError as e:
            err_msg = e.read().decode("utf-8")
            try:
                err_json = json.loads(err_msg)
                raise SlateError(err_json.get("error", f"HTTP {e.code}: {e.reason}"))
            except Exception:
                raise SlateError(f"HTTP {e.code}: {err_msg}")
        except Exception as e:
            raise SlateError(f"Failed to connect to Slate server at {self.endpoint}: {e}")

    def _get(self, path: str) -> Dict[str, Any]:
        url = f"{self.endpoint}{path}"
        req = urllib.request.Request(url, headers={"Accept": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                res_body = resp.read().decode("utf-8")
                return json.loads(res_body)
        except Exception as e:
            raise SlateError(f"Failed to connect to Slate server: {e}")

    def query(self, sql: str, tx_id: Optional[int] = None) -> QueryResult:
        """Executes an XQL query against Slate."""
        payload: Dict[str, Any] = {"sql": sql}
        if tx_id is not None:
            payload["tx_id"] = tx_id

        res = self._post("/v1/query", payload)
        return QueryResult(
            columns=res.get("columns", []),
            rows=res.get("rows", []),
            rows_affected=res.get("rows_affected", 0),
            execution_time_ms=res.get("execution_time_ms", 0.0),
            explain=res.get("explain")
        )

    def begin_transaction(self) -> Transaction:
        """Begins a new ACID snapshot-isolated transaction."""
        res = self._post("/v1/tx/begin", {})
        return Transaction(self, tx_id=res["tx_id"], read_timestamp=res["read_timestamp"])

    def transaction(self) -> Transaction:
        """Context manager for beginning a transaction with auto-commit or auto-abort."""
        return self.begin_transaction()

    def status(self) -> Dict[str, Any]:
        """Fetches runtime server status and metrics."""
        return self._get("/v1/status")

    def catalog(self) -> List[Dict[str, Any]]:
        """Fetches catalog tables and indexes."""
        return self._get("/v1/catalog").get("tables", [])
