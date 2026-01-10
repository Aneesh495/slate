"""
Slate Database Python SDK
Official client and PyArrow/NumPy bridge for Slate HTAP engine.
"""

from .client import SlateClient, QueryResult, Transaction, SlateError

__version__ = "1.0.0"
__all__ = ["SlateClient", "QueryResult", "Transaction", "SlateError"]
