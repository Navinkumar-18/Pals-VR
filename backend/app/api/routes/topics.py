"""Topic endpoints."""
from fastapi import APIRouter, Depends, Query
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.database.session import get_db
from app.models.topic import Topics
from app.schemas.common import ListResponse
from app.schemas.topic import TopicOut

router = APIRouter(prefix="/topics", tags=["topics"])


@router.get("", response_model=ListResponse[TopicOut])
def list_topics(
    limit: int = Query(100, ge=1, le=500),
    offset: int = Query(0, ge=0),
    db: Session = Depends(get_db),
):
    total = db.execute(select(func.count()).select_from(Topics)).scalar_one()
    rows = db.execute(select(Topics).order_by(Topics.id).limit(limit).offset(offset)).scalars().all()
    return ListResponse(
        items=[
            TopicOut(
                id=r.id,
                name=r.name,
                description=r.description or "",
                createdAt=r.created_at.isoformat(timespec="seconds") if r.created_at else "",
                updatedAt=r.updated_at.isoformat(timespec="seconds") if r.updated_at else "",
            )
            for r in rows
        ],
        count=total,
        limit=limit,
        offset=offset,
    )