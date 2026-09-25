"""Document CRUD + browse endpoints.

Public clients only ever receive PUBLISHED documents; every other status is
gated behind the admin key (Phase D scope: simple shared-key auth).

Phase E additions: POST /documents/upload (archival scan ingestion), the OCR
trigger/status/pages endpoints and the preserved-original download.
"""
import os
from typing import Optional

from fastapi import (
    APIRouter,
    Depends,
    File,
    Form,
    Header,
    HTTPException,
    Query,
    UploadFile,
)
from fastapi.responses import FileResponse
from sqlalchemy.orm import Session

from app.api.deps import require_admin
from app.config.settings import get_settings
from app.database.session import get_db
from app.models.enums import ArchivalStatus, RecordType
from app.models.ocr import OcrProcessingStatus
from app.models.relationship import Relationships
from app.repositories import document_repository as repo
from app.repositories import ocr_repository
from app.schemas.common import ListResponse, MessageResponse
from app.schemas.document import DocumentCreate, DocumentOut, DocumentUpdate
from app.schemas.ocr import OcrPageOut, OcrPageSummary, OcrStatusOut, UploadResponse
from app.services import document_service, ocr_service as ocr

router = APIRouter(prefix="/documents", tags=["documents"])

PUBLIC = ArchivalStatus.PUBLISHED.value

# Extension -> media type for the preserved-original download.
_ORIGINAL_MIME = {
    ".pdf": "application/pdf",
    ".jpg": "image/jpeg",
    ".jpeg": "image/jpeg",
    ".png": "image/png",
}


def _admin_sees_non_public(x_api_key) -> bool:
    """Only a MATCHING configured admin key unlocks non-published records.

    Public clients never receive non-PUBLISHED content, even when no key is
    configured in dev mode (the key only gates admin visibility/writes).
    """
    key = get_settings().admin_api_key
    return bool(key) and x_api_key == key


@router.get("", response_model=ListResponse[DocumentOut])
def list_documents(
    status: Optional[str] = Query(
        None, description="Archival filter; default PUBLISHED (public)"
    ),
    type: Optional[RecordType] = Query(None, description="Record type filter"),
    category: Optional[str] = None,
    language: Optional[str] = None,
    limit: int = Query(100, ge=1, le=500),
    offset: int = Query(0, ge=0),
    x_api_key: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    requested = (status or PUBLIC).upper()
    if requested != PUBLIC:
        if not _admin_sees_non_public(x_api_key):
            raise HTTPException(status_code=403, detail="Non-published statuses require admin access")
    rows, total = repo.list_documents(
        db,
        status=requested if requested != "all" else None,
        record_type=type,
        category=category,
        language=language,
        limit=limit,
        offset=offset,
    )
    items = [DocumentOut(**document_service.to_out_dict(db, r)) for r in rows]
    return ListResponse[DocumentOut](items=items, count=total, limit=limit, offset=offset)


@router.get("/{document_id}", response_model=DocumentOut)
def get_document(
    document_id: str,
    x_api_key: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    row = repo.get_document(db, document_id)
    if row is None or (row.status != PUBLIC and not _admin_sees_non_public(x_api_key)):
        raise HTTPException(status_code=404, detail="Document not found")
    return DocumentOut(**document_service.to_out_dict(db, row))


@router.post("", response_model=DocumentOut, status_code=201, dependencies=[Depends(require_admin)])
def create_document(payload: DocumentCreate, db: Session = Depends(get_db)):
    if repo.get_document(db, payload.id) is not None:
        raise HTTPException(status_code=409, detail=f"Document {payload.id!r} already exists")
    fields = {
        "id": payload.id,
        "type": payload.type,
        "title": payload.title,
        "description": payload.description,
        "category": payload.category,
        "date": payload.date,
        "language": payload.language,
        "source": payload.source,
        "status": payload.status,
        "is_sample_data": payload.is_sample_data,
        "citation": payload.citation,
        "event_id": payload.event_id,
        "ocr_text": payload.ocr_text,
        "tags": payload.tags,
    }
    media = [m.model_dump() for m in payload.media]
    doc = repo.create_document(db, fields, media)
    for target in payload.related_ids or []:
        if target != payload.id:
            db.add(Relationships(source_id=payload.id, target_id=target, relationship_type="related"))
    db.commit()
    db.refresh(doc)
    return DocumentOut(**document_service.to_out_dict(db, doc))


@router.put("/{document_id}", response_model=DocumentOut, dependencies=[Depends(require_admin)])
def update_document(
    document_id: str, payload: DocumentUpdate, db: Session = Depends(get_db)
):
    changes = payload.model_dump(exclude_unset=True)
    doc = repo.update_document(db, document_id, changes)
    if doc is None:
        raise HTTPException(status_code=404, detail="Document not found")
    db.commit()
    db.refresh(doc)
    return DocumentOut(**document_service.to_out_dict(db, doc))


@router.delete("/{document_id}", response_model=MessageResponse, dependencies=[Depends(require_admin)])
def delete_document(document_id: str, db: Session = Depends(get_db)):
    ocr.cleanup_original_on_delete(get_settings().storage_root, db, document_id)
    if not repo.delete_document(db, document_id):
        raise HTTPException(status_code=404, detail="Document not found")
    db.commit()
    return MessageResponse(message=f"Deleted {document_id}")


# ---------------------------------------------------------------------------
# Phase E: archival scan upload + OCR ingestion
# ---------------------------------------------------------------------------
def _require_document_or_404(db: Session, document_id: str, x_api_key: Optional[str]):
    """PUBLISHED docs are public; anything else needs the admin key."""
    row = repo.get_document(db, document_id)
    if row is None or (row.status != PUBLIC and not _admin_sees_non_public(x_api_key)):
        raise HTTPException(status_code=404, detail="Document not found")
    return row


def _build_ocr_status(db: Session, document_id: str) -> OcrStatusOut:
    summary = ocr.get_ocr_status(db, document_id) or {}
    pages = ocr.list_ocr_pages(db, document_id)
    return OcrStatusOut(
        documentId=summary.get("document_id", document_id),
        status=summary.get("status", "NONE"),
        pages=summary.get("pages", 0),
        language=summary.get("language", ""),
        error=summary.get("error"),
        pageSummaries=[
            OcrPageSummary(
                pageNumber=p["page_number"],
                status=p["status"],
                language=p["language"],
                confidence=p["confidence"],
            )
            for p in pages
        ],
    )


def _raise_upload_error(exc: ocr.UploadValidationError) -> None:
    raise HTTPException(status_code=exc.status_code, detail=exc.detail) from exc


@router.post(
    "/upload",
    response_model=UploadResponse,
    status_code=201,
    dependencies=[Depends(require_admin)],
)
async def upload_document(
    file: UploadFile = File(...),
    language: Optional[str] = Form(None),
    title: Optional[str] = Form(None),
    status: Optional[ArchivalStatus] = Form(None),
    db: Session = Depends(get_db),
):
    """Ingest an archival scan (PDF / JPEG / PNG).

    Validates extension + magic bytes + size, saves the original byte-for-byte
    under the storage root, creates the record (DRAFT by default) and starts
    OCR (inline when ocr_async=false, else on a background thread).
    """
    content = await file.read()
    effective_language = (language or get_settings().ocr_default_language).lower().strip()
    try:
        kind, ext = ocr.validate_upload(file.filename or "", content)
        ocr.require_engine_for_language(effective_language)
    except ocr.UploadValidationError as exc:
        _raise_upload_error(exc)

    document_id = ocr.generate_document_id()
    relative = ocr_repository.save_original(
        get_settings().storage_root, document_id, ext, content
    )
    fields = {
        "id": document_id,
        "type": RecordType.DOCUMENT.value,
        "title": title or f"Uploaded scan {document_id}",
        "description": (
            "Scanned archival document — OCR transcript generated by the archive "
            "pipeline from the preserved original."
        ),
        "category": "",
        "date": "",
        "language": effective_language,
        "source": "Archival scan upload",
        "status": (status or ArchivalStatus.DRAFT).value,
        "is_sample_data": False,
        "ocr_text": None,
        "citation": None,
        "event_id": None,
        "tags": [],
    }
    media = [ocr.build_upload_media(kind, relative, content)]
    try:
        doc = repo.create_document(db, fields, media)
    except Exception:
        # DB write failed -> remove the just-saved original so we never leave
        # orphaned files behind (original is only kept once a record exists).
        ocr_repository.delete_original(get_settings().storage_root, relative)
        raise
    doc.ocr_status = OcrProcessingStatus.PENDING
    doc.ocr_language = effective_language
    db.commit()
    db.refresh(doc)

    ocr.kick_off_ocr_job(db, document_id, effective_language)
    db.refresh(doc)
    return UploadResponse(
        document=DocumentOut(**document_service.to_out_dict(db, doc)),
        ocr=_build_ocr_status(db, document_id),
    )


@router.post(
    "/{document_id}/ocr",
    response_model=OcrStatusOut,
    response_model_exclude_none=True,
    dependencies=[Depends(require_admin)],
)
def run_ocr(
    document_id: str,
    language: Optional[str] = Form(None),
    db: Session = Depends(get_db),
):
    """(Re)run OCR for a document that has a stored original scan."""
    doc = repo.get_document(db, document_id)
    if doc is None:
        raise HTTPException(status_code=404, detail="Document not found")
    effective_language = (language or get_settings().ocr_default_language).lower().strip()
    try:
        ocr.ensure_original_available(db, document_id)
        ocr.require_engine_for_language(effective_language)
    except ocr.UploadValidationError as exc:
        _raise_upload_error(exc)

    doc.ocr_status = OcrProcessingStatus.PENDING
    doc.ocr_language = effective_language
    db.commit()
    ocr.kick_off_ocr_job(db, document_id, effective_language)
    return _build_ocr_status(db, document_id)


@router.get("/{document_id}/ocr", response_model=OcrStatusOut, response_model_exclude_none=True)
def get_ocr_status(
    document_id: str,
    x_api_key: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    """Document-level OCR state (status, page count, per-page summaries)."""
    _require_document_or_404(db, document_id, x_api_key)
    return _build_ocr_status(db, document_id)


@router.get(
    "/{document_id}/ocr/pages/{page_number}",
    response_model=OcrPageOut,
    response_model_exclude_none=True,
)
def get_ocr_page(
    document_id: str,
    page_number: int,
    x_api_key: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    """Full OCR text for one page of a document."""
    _require_document_or_404(db, document_id, x_api_key)
    page = ocr.get_ocr_page(db, document_id, page_number)
    if page is None:
        raise HTTPException(status_code=404, detail="OCR page not found")
    return OcrPageOut(
        documentId=page["document_id"],
        pageNumber=page["page_number"],
        status=page["status"],
        language=page["language"],
        confidence=page["confidence"],
        extractedText=page["extracted_text"],
        errorMessage=page["error_message"],
    )


@router.get("/{document_id}/original")
def get_original(
    document_id: str,
    x_api_key: Optional[str] = Header(default=None),
    db: Session = Depends(get_db),
):
    """Download the preserved original scan (byte-for-byte, read-only)."""
    _require_document_or_404(db, document_id, x_api_key)
    try:
        path = ocr.ensure_original_available(db, document_id)
    except ocr.UploadValidationError as exc:
        _raise_upload_error(exc)
    _, ext = os.path.splitext(path)
    return FileResponse(
        path,
        media_type=_ORIGINAL_MIME.get(ext.lower(), "application/octet-stream"),
        filename=os.path.basename(path),
    )