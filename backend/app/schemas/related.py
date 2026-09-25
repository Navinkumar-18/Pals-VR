"""Related-content response schemas.

``GET /api/related/{id}`` returns the relationships of a document with the
related documents embedded (each tagged with the relationship type), which is
exactly what the Unity UI needs to render "Related content".
"""
from pydantic import BaseModel

from app.schemas.common import ListResponse
from app.schemas.document import DocumentOut


class RelatedItem(BaseModel):
    relationshipType: str
    document: DocumentOut


class RelatedResponse(BaseModel):
    sourceId: str
    count: int
    items: list[RelatedItem]


RelatedListResponse = ListResponse[RelatedItem]