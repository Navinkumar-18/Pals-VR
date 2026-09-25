"""Event schemas."""
from typing import Optional

from pydantic import BaseModel


class EventOut(BaseModel):
    id: str
    title: str
    description: str = ""
    date: str = ""
    category: str = ""
    createdAt: str = ""
    updatedAt: str = ""


class EventIn(BaseModel):
    title: str
    description: str = ""
    date: str = ""
    category: str = ""