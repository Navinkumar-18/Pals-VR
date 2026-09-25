"""Person schemas."""
from typing import Optional

from pydantic import BaseModel


class PersonOut(BaseModel):
    id: str
    fullName: str
    birthYear: Optional[int] = None
    deathYear: Optional[int] = None
    description: str = ""
    createdAt: str = ""
    updatedAt: str = ""