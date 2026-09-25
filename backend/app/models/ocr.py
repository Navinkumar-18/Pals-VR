"""OCR result model (Phase E).

One row per extracted page of an ingested document:
  Document
    |-- Page 1  -> OcrResultRow(page_number=1, extracted_text, confidence)
    |-- Page 2  -> OcrResultRow(page_number=2, ...)
    |-- Page 3  -> ...

The document-level state (ocr_status / ocr_language / ocr_error) lives on the
``documents`` row so the status summary is a cheap single-row read; this table
keeps the page-level detail required by the paged OCR retrieval endpoints.
The original scan is never stored here — it lives in object/disk storage
referenced by the media row (see app.services.ocr_service.StorageService).
"""
from datetime import datetime, timezone

from sqlalchemy import DateTime, Float, ForeignKey, Integer, String, Text
from sqlalchemy.orm import Mapped, mapped_column

from app.database.base import Base


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


class OcrProcessingStatus:
    PENDING = "PENDING"
    PROCESSING = "PROCESSING"
    COMPLETED = "COMPLETED"
    FAILED = "FAILED"


class OcrResultRow(Base):
    __tablename__ = "ocr_results"

    # Stable id: "{document_id}-p{page_number}" (e.g. "AMB-DOC-001-p3").
    id: Mapped[str] = mapped_column(String(128), primary_key=True)
    document_id: Mapped[str] = mapped_column(
        ForeignKey("documents.id", ondelete="CASCADE"), index=True
    )
    page_number: Mapped[int] = mapped_column(Integer)
    extracted_text: Mapped[str] = mapped_column(Text, default="")
    language: Mapped[str] = mapped_column(String(16), default="en")
    confidence: Mapped[float | None] = mapped_column(Float, nullable=True)
    # OcrProcessingStatus value.
    processing_status: Mapped[str] = mapped_column(
        String(16), default=OcrProcessingStatus.PENDING, index=True
    )
    error_message: Mapped[str | None] = mapped_column(Text, nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    updated_at: Mapped[datetime] = mapped_column(
        DateTime, default=utcnow, onupdate=utcnow
    )