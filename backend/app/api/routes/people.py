"""People endpoints."""
from fastapi import APIRouter, Depends, Query
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.database.session import get_db
from app.models.person import People
from app.schemas.common import ListResponse
from app.schemas.person import PersonOut

router = APIRouter(prefix="/people", tags=["people"])


def _to_out(row: People) -> dict:
    return {
        "id": row.id,
        "fullName": row.full_name,
        "birthYear": row.birth_year,
        "deathYear": row.death_year,
        "description": row.description or "",
        "createdAt": row.created_at.isoformat(timespec="seconds") if row.created_at else "",
        "updatedAt": row.updated_at.isoformat(timespec="seconds") if row.updated_at else "",
    }


@router.get("", response_model=ListResponse[PersonOut])
def list_people(
    limit: int = Query(100, ge=1, le=500),
    offset: int = Query(0, ge=0),
    db: Session = Depends(get_db),
):
    total = db.execute(select(func.count()).select_from(People)).scalar_one()
    rows = db.execute(select(People).order_by(People.id).limit(limit).offset(offset)).scalars().all()
    return ListResponse(
        items=[PersonOut(**_to_out(r)) for r in rows], count=total, limit=limit, offset=offset
    )