"""Event endpoints."""
from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.database.session import get_db
from app.models.event import Events
from app.schemas.common import ListResponse
from app.schemas.event import EventOut

router = APIRouter(prefix="/events", tags=["events"])


def _to_out(row: Events) -> dict:
    return {
        "id": row.id,
        "title": row.title,
        "description": row.description or "",
        "date": row.date or "",
        "category": row.category or "",
        "createdAt": row.created_at.isoformat(timespec="seconds") if row.created_at else "",
        "updatedAt": row.updated_at.isoformat(timespec="seconds") if row.updated_at else "",
    }


@router.get("", response_model=ListResponse[EventOut])
def list_events(
    limit: int = Query(100, ge=1, le=500),
    offset: int = Query(0, ge=0),
    db: Session = Depends(get_db),
):
    total = db.execute(select(func.count()).select_from(Events)).scalar_one()
    rows = (
        db.execute(select(Events).order_by(Events.id).limit(limit).offset(offset)).scalars().all()
    )
    return ListResponse(
        items=[EventOut(**_to_out(r)) for r in rows], count=total, limit=limit, offset=offset
    )


@router.get("/{event_id}", response_model=EventOut)
def get_event(event_id: str, db: Session = Depends(get_db)):
    row = db.get(Events, event_id)
    if row is None:
        raise HTTPException(status_code=404, detail="Event not found")
    return EventOut(**_to_out(row))