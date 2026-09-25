"""Document data-access functions (SQLAlchemy 2.0 style)."""
from typing import List, Optional, Sequence, Set

from sqlalchemy import func, or_, select
from sqlalchemy.orm import Session

from app.models.document import Documents, Media
from app.models.enums import ArchivalStatus, RecordType
from app.models.relationship import Relationships

PUBLIC_STATUS = ArchivalStatus.PUBLISHED.value


def list_documents(
    db: Session,
    *,
    status: Optional[str] = None,
    record_type: Optional[RecordType] = None,
    category: Optional[str] = None,
    language: Optional[str] = None,
    limit: int = 100,
    offset: int = 0,
) -> tuple[List[Documents], int]:
    stmt = select(Documents)
    count_stmt = select(func.count()).select_from(Documents)
    if status:
        stmt = stmt.where(Documents.status == status)
        count_stmt = count_stmt.where(Documents.status == status)
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


def get_document(db: Session, document_id: str) -> Optional[Documents]:
    return db.get(Documents, document_id)


def create_document(db: Session, fields: dict, media: List[dict]) -> Documents:
    doc = Documents(**fields)
    db.add(doc)
    db.flush()  # assign id before creating media rows
    for i, m in enumerate(media):
        media_id = m.get("id") or f"{doc.id}-med-{i + 1}"
        db.add(
            Media(
                id=media_id,
                document_id=doc.id,
                media_type=m["media_type"],
                reference=m["reference"],
                mime_type=m.get("mime_type"),
                size_bytes=m.get("size_bytes"),
                caption=m.get("caption"),
                sort_order=m.get("sort_order", 0),
            )
        )
    db.flush()
    db.refresh(doc)
    return doc


def update_document(db: Session, document_id: str, changes: dict) -> Optional[Documents]:
    doc = get_document(db, document_id)
    if doc is None:
        return None
    for key, value in changes.items():
        setattr(doc, key, value)
    db.flush()
    db.refresh(doc)
    return doc


def delete_document(db: Session, document_id: str) -> bool:
    doc = get_document(db, document_id)
    if doc is None:
        return False
    # Remove relationship rows that touch this document (flexible model has
    # no FK, so clean up explicitly).
    db.execute(
        Relationships.__table__.delete().where(
            or_(
                Relationships.source_id == document_id,
                Relationships.target_id == document_id,
            )
        )
    )
    db.delete(doc)
    db.flush()
    return True


def published_document_ids(db: Session, candidate_ids: Set[str]) -> Set[str]:
    """Subset of candidate ids that are PUBLISHED documents."""
    if not candidate_ids:
        return set()
    rows = db.execute(
        select(Documents.id).where(
            Documents.id.in_(candidate_ids),
            Documents.status == PUBLIC_STATUS,
        )
    ).scalars().all()
    return set(rows)


def document_ids_from_relationships(db: Session, document_id: str) -> Set[str]:
    """Other-side entity ids for every relationship touching a document."""
    rows = db.execute(
        select(Relationships).where(
            or_(
                Relationships.source_id == document_id,
                Relationships.target_id == document_id,
            )
        )
    ).scalars().all()
    candidates: Set[str] = set()
    for r in rows:
        other = r.target_id if r.source_id == document_id else r.source_id
        candidates.add(other)
    return candidates


def relationships_for(db: Session, document_id: str) -> List[tuple[str, str]]:
    """All (relationship_type, other_entity_id) pairs touching a document,
    deduplicated, preserving direction."""
    rows = db.execute(
        select(Relationships).where(
            or_(
                Relationships.source_id == document_id,
                Relationships.target_id == document_id,
            )
        )
    ).scalars().all()
    seen: set[tuple[str, str]] = set()
    out: List[tuple[str, str]] = []
    for r in rows:
        other = r.target_id if r.source_id == document_id else r.source_id
        pair = (r.relationship_type, other)
        if pair not in seen:
            seen.add(pair)
            out.append(pair)
    return out