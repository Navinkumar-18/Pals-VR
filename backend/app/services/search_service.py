"""Metadata search (Phase D scope).

Phase D search filters on metadata (title/description/filters); semantic
search over embeddings is deferred to Phase F.
"""
from typing import List, Optional

from sqlalchemy import func, or_, select
from sqlalchemy.orm import Session

from app.models.document import Documents
from app.models.enums import ArchivalStatus, RecordType

PUBLIC_STATUS = ArchivalStatus.PUBLISHED.value


def search_documents(
    db: Session,
    *,
    q: Optional[str] = None,
    record_type: Optional[RecordType] = None,
    category: Optional[str] = None,
    language: Optional[str] = None,
    status: str = PUBLIC_STATUS,
    limit: int = 100,
    offset: int = 0,
) -> tuple[List[Documents], int]:
    stmt = select(Documents)
    count_stmt = select(func.count()).select_from(Documents)

    stmt = stmt.where(Documents.status == status)
    count_stmt = count_stmt.where(Documents.status == status)

    if q:
        pattern = f"%{q}%"
        match = or_(
            Documents.title.ilike(pattern),
            Documents.description.ilike(pattern),
            Documents.ocr_text.ilike(pattern),
        )
        stmt = stmt.where(match)
        count_stmt = count_stmt.where(match)
    if record_type:
        stmt = stmt.where(Documents.type == record_type.value)
        count_stmt = count_stmt.where(Documents.type == record_type.value)
    if category:
        stmt = stmt.where(Documents.category == category)
        count_stmt = count_stmt.where(Documents.category == category)
    if language:
        stmt = stmt.where(Documents.language == language)
        count_stmt = count_stmt.where(Documents.language == language)

    total = db.execute(count_stmt).scalar_one()
    rows = (
        db.execute(stmt.order_by(Documents.id).limit(limit).offset(offset))
        .scalars()
        .all()
    )
    return list(rows), total