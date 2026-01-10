import unittest
from slate.client import QueryResult, SlateError

class TestQueryResult(unittest.TestCase):
    def test_query_result_formatting_and_dict(self):
        cols = ["id", "name", "embedding"]
        rows = [
            [1, "Alice", [0.1, 0.2, 0.3]],
            [2, "Bob", [0.4, 0.5, 0.6]]
        ]
        res = QueryResult(cols, rows, rows_affected=2, execution_time_ms=1.45)

        self.assertEqual(len(res), 2)
        d = res.to_dict()
        self.assertEqual(d["id"], [1, 2])
        self.assertEqual(d["name"], ["Alice", "Bob"])
        self.assertEqual(d["embedding"][0], [0.1, 0.2, 0.3])

    def test_query_result_iteration(self):
        cols = ["x"]
        rows = [[10], [20], [30]]
        res = QueryResult(cols, rows, rows_affected=3, execution_time_ms=0.5)

        collected = [r[0] for r in res]
        self.assertEqual(collected, [10, 20, 30])

    def test_slate_error_exception(self):
        with self.assertRaises(SlateError):
            raise SlateError("Table not found")

if __name__ == "__main__":
    unittest.main()
