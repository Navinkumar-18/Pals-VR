"""Related-content service: relationships -> related PUBLISHED documents."""
from typing import List, Tuple

from sqlalchemy.orm import Session

from app.models.document import Documents
from app.repositories import document_repository as repo


def get_related(db: Session, document_id: str) -> List[Tuple[str, Documents]]:
    """Return (relationship_type, related_document) pairs for a document.

    Only PUBLISHED related documents are returned to public clients; the
    relationship type is exposed so the UI can label the link.
    """
    pairs = repo.relationships_for(db, document_id)
    out: List[Tuple[str, Documents]] = []
    seen_docs: set[str] = set()
    for rel_type, other_id in pairs:
        other = repo.get_document(db, other_id)
        if other is None or other.status != "PUBLISHED":
            continue
        if other.id in seen_docs:
            continue
        seen_docs.add(other.id)
        out.append((rel_type, other))
    return out