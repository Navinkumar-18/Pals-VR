"""OCR document ingestion pipeline (Phase E).

Flow:  upload -> validate -> save original -> create record -> OCR job ->
       cleaned per-page text rows -> aggregated document.ocr_text.

The original scan is preserved byte-for-byte on disk and never modified; OCR
output is derived text stored separately. All file names are server-generated
and path-traversal is blocked by the repository helpers.
"""
from __future__ import annotations

import logging
import os
import threading
import uuid
from typing import Optional

from sqlalchemy.orm import Session

from app.config.settings import get_settings
from app.models.document import Documents, Media
from app.models.enums import MediaType, RecordType
from app.models.ocr import OcrProcessingStatus
from app.ocr import (
    OcrEngineUnavailableError,
    OcrError,
    OcrLanguageUnavailableError,
    get_ocr_engine,
)
from app.ocr.cleaner import aggregate_pages, clean_text
from app.repositories import document_repository, ocr_repository

logger = logging.getLogger("ambedkar.api.ocr")

# File-type sniffing (magic bytes) — never trust the client filename alone.
_MAGIC = {
    "pdf": b"%PDF",
    "jpg": b"\xff\xd8\xff",
    "jpeg": b"\xff\xd8\xff",
    "png": b"\x89PNG\r\n\x1a\n",
}

EXTENSION_TO_KIND = {
    ".pdf": "pdf",
    ".jpg": "jpg",
    ".jpeg": "jpeg",
    ".png": "png",
}

KIND_TO_MEDIA_TYPE = {
    "pdf": MediaType.PDF,
    "jpg": MediaType.DOCUMENT_SCAN,
    "jpeg": MediaType.DOCUMENT_SCAN,
    "png": MediaType.DOCUMENT_SCAN,
}

KIND_TO_MIME = {
    "pdf": "application/pdf",
    "jpg": "image/jpeg",
    "jpeg": "image/jpeg",
    "png": "image/png",
}


class UploadValidationError(Exception):
    """Raised with (http_status, detail) for malformed uploads."""

    def __init__(self, status_code: int, detail: str) -> None:
        super().__init__(detail)
        self.status_code = status_code
        self.detail = detail


def validate_upload(filename: str, content: bytes) -> tuple[str, str]:
    """Validate extension + magic bytes + size. Returns (kind, ext)."""
    if not filename:
        raise UploadValidationError(400, "Missing filename.")

    ext = (os.path.splitext(filename or "")[1] or "").lower()
    if ext not in EXTENSION_TO_KIND:
        raise UploadValidationError(
            415,
            f"Unsupported file type '{ext or 'none'}'. Allowed: .pdf, .jpg, .jpeg, .png",
        )

    max_bytes = get_settings().max_upload_mb * 1024 * 1024
    if len(content) > max_bytes:
        raise UploadValidationError(
            413,
            f"File exceeds the {get_settings().max_upload_mb} MB upload limit.",
        )

    kind = EXTENSION_TO_KIND[ext]
    magic = _MAGIC[kind]
    if not content[: len(magic)] == magic:
        raise UploadValidationError(
            400, "File content does not match its extension (corrupted or renamed file)."
        )
    return kind, ext


def generate_document_id() -> str:
    """Stable server-side id for an uploaded document (never client-derived)."""
    return "AMB-UPL-" + uuid.uuid4().hex[:8].upper()


def require_engine_for_language(language: str) -> None:
    """Synchronously reject a language the active engine cannot OCR.

    Raises 400 with a clear configuration message (never silent fallback);
    raises 503 when no engine can be loaded at all.
    """
    try:
        engine = get_ocr_engine()
    except OcrEngineUnavailableError as exc:
        raise UploadValidationError(503, str(exc)) from exc
    try:
        engine.require_language(language)
    except OcrLanguageUnavailableError as exc:
        raise UploadValidationError(400, str(exc)) from exc


def build_upload_media(kind: str, relative: str, content: bytes) -> dict:
    """Media-row payload for a freshly persisted original scan."""
    return {
        "media_type": KIND_TO_MEDIA_TYPE[kind].value,
        "reference": relative,
        "mime_type": KIND_TO_MIME[kind],
        "size_bytes": len(content),
        "caption": "Original scan (preserved byte-for-byte)",
        "sort_order": 0,
    }


def _original_media(db: Session, document_id: str) -> Optional[Media]:
    """The stored original media row for a document (PDF/scan/image), if any."""
    document = document_repository.get_document(db, document_id)
    if document is None or not document.media:
        return None
    for media in sorted(document.media, key=lambda m: (m.sort_order, m.id)):
        if media.media_type in (
            MediaType.PDF.value,
            MediaType.DOCUMENT_SCAN.value,
            MediaType.IMAGE.value,
        ):
            return media
    return None


def ensure_original_available(db: Session, document_id: str) -> str:
    """Resolve the absolute path of a document's original scan.

    Raises UploadValidationError(404) when the document has no local original
    and (422) when the file is missing/cannot be resolved — used so a POST
    OCR trigger on a record without stored originals fails fast with a clear
    message instead of a doomed background job.
    """
    media = _original_media(db, document_id)
    if media is None:
        raise UploadValidationError(404, "Document has no stored original scan.")
    try:
        return ocr_repository.resolve_original_path(
            get_settings().storage_root, media.reference
        )
    except ocr_repository.StorageError as exc:
        raise UploadValidationError(422, str(exc)) from exc


def _run_ocr_job_unchecked(db: Session, document_id: str, language: str) -> None:
    """Execute OCR for a document inside a caller-owned session.

    Sets the document-level state (PROCESSING -> COMPLETED/FAILED) and writes
    one OcrResultRow per page. The original file is only ever read.
    """
    document = document_repository.get_document(db, document_id)
    if document is None:
        logger.warning("OCR job skipped: document %s missing.", document_id)
        return

    document.ocr_status = OcrProcessingStatus.PROCESSING
    db.commit()

    engine = get_ocr_engine()
    try:
        media = _original_media(db, document_id)
        if media is None:
            raise OcrError("Original file media row missing.")
        original_path = ocr_repository.resolve_original_path(
            get_settings().storage_root, media.reference
        )
        with open(original_path, "rb") as handle:
            original_bytes = handle.read()

        is_pdf = media.media_type == MediaType.PDF.value
        if is_pdf:
            results = engine.ocr_pdf(original_bytes, language)
        else:
            results = [engine.ocr_image(original_bytes, language)]

        # Clean each page and persist per-page rows (previous run is replaced).
        ocr_repository.delete_ocr_results(db, document_id)
        cleaned_pages: list[str] = []
        pages: list = []
        for result in results:
            text = clean_text(result.text)
            cleaned_pages.append(text)
            pages.append((result, text))

        for result, text in pages:
            ocr_repository.upsert_ocr_page(
                db,
                document_id=document_id,
                page_number=result.page_number,
                extracted_text=text,
                language=result.language or language,
                confidence=result.confidence,
                processing_status=OcrProcessingStatus.COMPLETED,
                error_message=None,
            )

        document.ocr_text = aggregate_pages(cleaned_pages)
        document.ocr_language = language
        document.ocr_error = None
        document.ocr_status = (
            OcrProcessingStatus.COMPLETED if cleaned_pages else OcrProcessingStatus.FAILED
        )
        if not cleaned_pages:
            document.ocr_error = "OCR produced no text (empty result)."
        db.commit()
        logger.info(
            "OCR COMPLETED for %s (%d page(s), lang=%s, engine=%s)",
            document_id,
            len(pages),
            language,
            engine.name,
        )
    except Exception as exc:
        db.rollback()
        document = document_repository.get_document(db, document_id)
        if document is not None:
            document.ocr_status = OcrProcessingStatus.FAILED
            document.ocr_error = str(exc)[:1000]
            db.commit()
        logger.error("OCR FAILED for %s: %s", document_id, exc)


def kick_off_ocr_job(db: Session, document_id: str, language: str) -> None:
    """Start OCR — inline when ocr_async is false (tests), else background
    thread. The caller's session must be committed before calling (the worker
    uses its own session when async)."""
    require_engine_for_language(language)
    if get_settings().ocr_async:
        def worker() -> None:
            from app.database.session import SessionLocal

            session = SessionLocal()
            try:
                _run_ocr_job_unchecked(session, document_id, language)
            finally:
                session.close()

        threading.Thread(target=worker, daemon=True, name=f"ocr-{document_id}").start()
    else:
        _run_ocr_job_unchecked(db, document_id, language)


def get_ocr_status(db: Session, document_id: str) -> Optional[dict]:
    """{document_id, status, pages, language, error} summary or None."""
    document = document_repository.get_document(db, document_id)
    if document is None:
        return None
    return {
        "document_id": document_id,
        "status": document.ocr_status or "NONE",
        "pages": ocr_repository.count_ocr_pages(db, document_id),
        "language": document.ocr_language or document.language or "",
        "error": document.ocr_error,
    }


def get_ocr_page(db: Session, document_id: str, page_number: int) -> Optional[dict]:
    row = ocr_repository.get_ocr_page(db, document_id, page_number)
    if row is None:
        return None
    return {
        "document_id": document_id,
        "page_number": row.page_number,
        "status": row.processing_status,
        "language": row.language,
        "confidence": row.confidence,
        "extracted_text": row.extracted_text or "",
        "error_message": row.error_message,
    }


def list_ocr_pages(db: Session, document_id: str) -> list[dict]:
    rows = ocr_repository.list_ocr_results(db, document_id)
    return [
        {
            "document_id": document_id,
            "page_number": row.page_number,
            "status": row.processing_status,
            "language": row.language,
            "confidence": row.confidence,
        }
        for row in rows
    ]


def cleanup_original_on_delete(storage_root: str, db: Session, document_id: str) -> None:
    """Best-effort removal of the stored original after a document delete."""
    document = document_repository.get_document(db, document_id)
    if document is None or not document.media:
        return
    for media in document.media:
        if media.media_type in (
            MediaType.PDF.value,
            MediaType.DOCUMENT_SCAN.value,
            MediaType.IMAGE.value,
        ):
            ocr_repository.delete_original(storage_root, media.reference)