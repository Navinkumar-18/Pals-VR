"""Related-content endpoint."""
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from app.database.session import get_db
from app.models.enums import ArchivalStatus
from app.repositories import document_repository as repo
from app.schemas.document import DocumentOut
from app.schemas.related import RelatedItem, RelatedResponse
from app.services import document_service, related_service

router = APIRouter(prefix="/related", tags=["related"])


@router.get("/{document_id}", response_model=RelatedResponse)
def related(document_id: str, db: Session = Depends(get_db)):
    doc = repo.get_document(db, document_id)
    if doc is None or doc.status != ArchivalStatus.PUBLISHED.value:
        raise HTTPException(status_code=404, detail="Document not found")
    pairs = related_service.get_related(db, document_id)
    items = [
        RelatedItem(
            relationshipType=rel_type,
            document=DocumentOut(**document_service.to_out_dict(db, related_doc)),
        )
        for rel_type, related_doc in pairs
    ]
    return RelatedResponse(sourceId=document_id, count=len(items), items=items)