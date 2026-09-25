"""Metadata search endpoint (Phase D scope)."""
from typing import Optional

from fastapi import APIRouter, Depends, Header, HTTPException, Query
from sqlalchemy.orm import Session

from app.config.settings import get_settings
from app.database.session import get_db
from app.models.enums import ArchivalStatus, RecordType
from app.schemas.common import ListResponse
from app.schemas.document import DocumentOut
from app.services import document_service, search_service

router = APIRouter(prefix="/search", tags=["search"])

PUBLIC = ArchivalStatus.PUBLISHED.value


@router.get("", response_model=ListResponse[DocumentOut])
def search(
    q: str = Query("", description="Text match on title/description"),
    type: Optional[RecordType] = Query(None),
    category: Optional[str] = None,
    language: Optional[str] = None,
    status: Optional[str] = Query(None, description="Defaults to PUBLISHED for public clients"),
    limit: int = Query(100, ge=1, le=500),
    offset: int = Query(0, ge=0),
    x_api_key: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    requested = (status or PUBLIC).upper()
    if requested != PUBLIC:
        key = get_settings().admin_api_key
        if key and x_api_key != key:
            raise HTTPException(status_code=403, detail="Non-published statuses require admin access")
    rows, total = search_service.search_documents(
        db,
        q=q.strip() or None,
        record_type=type,
        category=category,
        language=language,
        status=requested,
        limit=limit,
        offset=offset,
    )
    items = [DocumentOut(**document_service.to_out_dict(db, r)) for r in rows]
    return ListResponse[DocumentOut](items=items, count=total, limit=limit, offset=offset)