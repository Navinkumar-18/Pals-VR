"""OCR data-access + original-file storage (Phase E).

Originals live on disk under ``{storage_root}/originals/{document_id}.{ext}``
and are preserved byte-for-byte; the DB only keeps the relative reference on
the media row. All file names are server-generated from the stable document id
(never from client input) and every resolved path is containment-checked
against the storage root (path-traversal protection).
"""
from __future__ import annotations

import os
import shutil
from typing import Optional, Sequence

from sqlalchemy import select
from sqlalchemy.orm import Session

from app.models.document import Documents
from app.models.ocr import OcrProcessingStatus, OcrResultRow

_ORIGINALS_DIR = "originals"

ALLOWED_EXTENSIONS = {".pdf", ".jpg", ".jpeg", ".png"}


# ---------------------------------------------------------------------------
# File storage (original preservation; never modified after upload)
# ---------------------------------------------------------------------------
class StorageError(Exception):
    pass


def storage_originals_dir(storage_root: str) -> str:
    return os.path.join(storage_root, _ORIGINALS_DIR)


def original_relative_path(document_id: str, ext: str) -> str:
    """Server-generated relative media reference (never client-derived)."""
    safe_ext = ext.lower() if ext.lower() in ALLOWED_EXTENSIONS else ".bin"
    return f"{_ORIGINALS_DIR}/{document_id}{safe_ext}"


def save_original(storage_root: str, document_id: str, ext: str, content: bytes) -> str:
    """Persist the uploaded original under the storage root.

    Returns the relative path used as the media reference. Raises StorageError
    when the file cannot be written.
    """
    originals_dir = storage_originals_dir(storage_root)
    os.makedirs(originals_dir, exist_ok=True)
    relative = original_relative_path(document_id, ext)
    destination = os.path.realpath(os.path.join(storage_root, relative))
    root_real = os.path.realpath(storage_root)
    if not (destination == root_real or destination.startswith(root_real + os.sep)):
        raise StorageError("Resolved path escapes the storage root.")
    try:
        with open(destination, "wb") as handle:
            handle.write(content)
    except OSError as exc:  # pragma: no cover - filesystem failure
        raise StorageError(f"Could not write original file: {exc}") from exc
    return relative


def resolve_original_path(storage_root: str, relative_reference: str) -> str:
    """Resolve a media reference to an absolute, containment-checked path."""
    if not relative_reference or relative_reference.startswith(("http://", "https://")):
        raise StorageError("Original reference is not a local file.")
    root_real = os.path.realpath(storage_root)
    resolved = os.path.realpath(os.path.join(root_real, relative_reference))
    if not (resolved == root_real or resolved.startswith(root_real + os.sep)):
        raise StorageError("Original reference escapes the storage root.")
    if not os.path.isfile(resolved):
        raise StorageError("Original file not found on disk.")
    return resolved


def delete_original(storage_root: str, relative_reference: str) -> None:
    """Remove a stored original (called when its document is deleted)."""
    try:
        path = resolve_original_path(storage_root, relative_reference)
    except StorageError:
        return
    try:
        os.remove(path)
    except OSError:  # pragma: no cover - best effort cleanup
        pass


# ---------------------------------------------------------------------------
# OCR result rows
# ---------------------------------------------------------------------------
def ocr_page_id(document_id: str, page_number: int) -> str:
    return f"{document_id}-p{page_number}"


def upsert_ocr_page(
    db: Session,
    *,
    document_id: str,
    page_number: int,
    extracted_text: str,
    language: str,
    confidence: Optional[float],
    processing_status: str = OcrProcessingStatus.COMPLETED,
    error_message: Optional[str] = None,
) -> OcrResultRow:
    row_id = ocr_page_id(document_id, page_number)
    row = db.get(OcrResultRow, row_id)
    if row is None:
        row = OcrResultRow(id=row_id, document_id=document_id, page_number=page_number)
        db.add(row)
    row.extracted_text = extracted_text
    row.language = language
    row.confidence = confidence
    row.processing_status = processing_status
    row.error_message = error_message
    db.flush()
    return row


def delete_ocr_results(db: Session, document_id: str) -> None:
    db.execute(
        OcrResultRow.__table__.delete().where(
            OcrResultRow.document_id == document_id
        )
    )
    db.flush()


def list_ocr_results(db: Session, document_id: str) -> Sequence[OcrResultRow]:
    return db.execute(
        select(OcrResultRow)
        .where(OcrResultRow.document_id == document_id)
        .order_by(OcrResultRow.page_number)
    ).scalars().all()


def get_ocr_page(db: Session, document_id: str, page_number: int) -> Optional[OcrResultRow]:
    return db.get(OcrResultRow, ocr_page_id(document_id, page_number))


def count_ocr_pages(db: Session, document_id: str) -> int:
    from sqlalchemy import func

    return (
        db.execute(
            select(func.count())
            .select_from(OcrResultRow)
            .where(OcrResultRow.document_id == document_id)
        ).scalar_one()
    )


def completed_ocr_pages(db: Session, document_id: str) -> int:
    from sqlalchemy import func

    return (
        db.execute(
            select(func.count())
            .select_from(OcrResultRow)
            .where(
                OcrResultRow.document_id == document_id,
                OcrResultRow.processing_status == OcrProcessingStatus.COMPLETED,
            )
        ).scalar_one()
    )