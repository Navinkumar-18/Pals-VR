"""OCR request/response schemas (Phase E).

CamelCase field names match the Unity JSON contract (same convention as
``DocumentOut``) so the VR app can deserialize OCR payloads directly.
"""
from typing import List, Optional

from pydantic import BaseModel, Field

from app.schemas.document import DocumentOut


class OcrPageSummary(BaseModel):
    """Lightweight per-page entry inside an OCR status payload."""

    pageNumber: int
    status: str
    language: str = "en"
    confidence: Optional[float] = None


class OcrStatusOut(BaseModel):
    """Document-level OCR state + one summary per page."""

    documentId: str
    status: str  # NONE | PENDING | PROCESSING | COMPLETED | FAILED
    pages: int = 0
    language: str = ""
    error: Optional[str] = None
    pageSummaries: List[OcrPageSummary] = Field(default_factory=list)


class OcrPageOut(BaseModel):
    """Full OCR result for a single page (retrieval endpoint)."""

    documentId: str
    pageNumber: int
    status: str
    language: str = "en"
    confidence: Optional[float] = None
    extractedText: str = ""
    errorMessage: Optional[str] = None


class OcrLanguageOut(BaseModel):
    """Languages an engine can actually serve (configuration introspection)."""

    default: str
    supported: List[str]


class UploadResponse(BaseModel):
    """Response of POST /documents/upload: record + OCR state."""

    document: DocumentOut
    ocr: OcrStatusOut