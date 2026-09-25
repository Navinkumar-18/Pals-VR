"""Shared, reusable schema pieces."""
from typing import Generic, TypeVar

from pydantic import BaseModel, ConfigDict

T = TypeVar("T")


class ListResponse(BaseModel, Generic[T]):
    """Wrapper for every list endpoint (JsonUtility-friendly)."""

    items: list[T]
    count: int
    limit: int
    offset: int


class MessageResponse(BaseModel):
    message: str