"""Media endpoint (metadata + file references only; binaries never in DB)."""
from typing import Optional

from fastapi import APIRouter, Depends, Query
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.database.session import get_db
from app.models.document import Media
from app.models.enums import MediaType
from app.schemas.common import ListResponse
from app.schemas.document import MediaOut

router = APIRouter(prefix="/media", tags=["media"])


def _to_out(row: Media) -> dict:
    return {
        "id": row.id,
        "documentId": row.document_id,
        "mediaType": row.media_type,
        "reference": row.reference,
        "mimeType": row.mime_type,
        "sizeBytes": row.size_bytes,
        "caption": row.caption,
        "sortOrder": row.sort_order,
    }


@router.get("", response_model=ListResponse[MediaOut])
def list_media(
    media_type: Optional[MediaType] = Query(None, description="Filter by type"),
    document_id: Optional[str] = Query(None, description="Filter by document"),
    limit: int = Query(100, ge=1, le=500),
    offset: int = Query(0, ge=0),
    db: Session = Depends(get_db),
):
    stmt = select(Media)
    count_stmt = select(func.count()).select_from(Media)
    if media_type:
        stmt = stmt.where(Media.media_type == media_type.value)
        count_stmt = count_stmt.where(Media.media_type == media_type.value)
    if document_id:
        stmt = stmt.where(Media.document_id == document_id)
        count_stmt = count_stmt.where(Media.document_id == document_id)
    total = db.execute(count_stmt).scalar_one()
    rows = db.execute(stmt.order_by(Media.document_id, Media.sort_order).limit(limit).offset(offset)).scalars().all()
    return ListResponse(
        items=[MediaOut(**_to_out(r)) for r in rows], count=total, limit=limit, offset=offset
    )