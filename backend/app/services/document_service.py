"""Business logic: mapping documents to the Unity JSON contract."""
from datetime import datetime
from typing import List, Optional, Set

from sqlalchemy.orm import Session

from app.models.document import Documents, Media
from app.models.enums import ArchivalStatus, MediaType, RecordType
from app.repositories import document_repository as repo
from app.repositories import ocr_repository

VERIFIED_STATUSES: Set[str] = {
    ArchivalStatus.VERIFIED.value,
    ArchivalStatus.PUBLISHED.value,
}


def _iso(dt: Optional[datetime]) -> str:
    return dt.isoformat(timespec="seconds") if dt else ""


def _media_refs(doc: Documents) -> dict:
    """Derive the Unity convenience fields (image/audio/video/document/pages)
    from the media rows, plus the full media ref list."""
    image = audio = video = document_ref = None
    pages: List[str] = []
    refs = []
    ordered = sorted(doc.media, key=lambda m: (m.sort_order, m.id))
    for m in ordered:
        refs.append(
            {
                "id": m.id,
                "mediaType": m.media_type,
                "reference": m.reference,
                "mimeType": m.mime_type,
                "caption": m.caption,
            }
        )
        if m.media_type == MediaType.IMAGE.value:
            if image is None:
                image = m.reference
            if m.reference not in pages:
                pages.append(m.reference)
        elif m.media_type == MediaType.DOCUMENT_SCAN.value:
            if document_ref is None:
                document_ref = m.reference
            if m.reference not in pages:
                pages.append(m.reference)
        elif m.media_type == MediaType.PDF.value:
            if document_ref is None:
                document_ref = m.reference
        elif m.media_type == MediaType.AUDIO.value:
            if audio is None:
                audio = m.reference
        elif m.media_type == MediaType.VIDEO.value:
            if video is None:
                video = m.reference
    return {
        "image": image,
        "audio": audio,
        "video": video,
        "document": document_ref,
        "pages": pages,
        "media": refs,
    }


def to_out_dict(db: Session, doc: Documents) -> dict:
    """Translate a Documents row into the camelCase Unity-compatible dict."""
    candidate_ids = repo.document_ids_from_relationships(db, doc.id)
    related = sorted(repo.published_document_ids(db, candidate_ids))
    media = _media_refs(doc)
    return {
        "id": doc.id,
        "type": RecordType(doc.type).unity_value,
        "title": doc.title,
        "description": doc.description or "",
        "date": doc.date or "",
        "category": doc.category or "",
        "source": doc.source or "",
        "language": doc.language or "",
        "isSampleData": bool(doc.is_sample_data),
        "verified": doc.status in VERIFIED_STATUSES,
        "status": doc.status,
        "citation": doc.citation,
        "eventId": doc.event_id,
        "ocrText": doc.ocr_text,
        "ocrStatus": doc.ocr_status or "NONE",
        "ocrLanguage": doc.ocr_language or doc.language or "",
        "ocrPages": ocr_repository.count_ocr_pages(db, doc.id),
        "tags": doc.tags or [],
        "relatedIds": related,
        "image": media["image"],
        "audio": media["audio"],
        "video": media["video"],
        "document": media["document"],
        "pages": media["pages"],
        "media": media["media"],
        "createdAt": _iso(doc.created_at),
        "updatedAt": _iso(doc.updated_at),
    }