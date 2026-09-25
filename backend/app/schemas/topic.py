"""Topic schemas."""
from pydantic import BaseModel


class TopicOut(BaseModel):
    id: str
    name: str
    description: str = ""
    createdAt: str = ""
    updatedAt: str = ""