"""Document request/response schemas.

The OUT schema mirrors Unity's ``ArchiveRecord`` JSON contract field-for-field
(camelCase, numeric ``type``) so the VR app can deserialize it directly.
"""
from typing import List, Optional

from pydantic import BaseModel, ConfigDict, Field

from app.models.enums import ArchivalStatus, MediaType, RecordType


# ---------------------------------------------------------------------------
# Media
# ---------------------------------------------------------------------------
class MediaCreate(BaseModel):
    id: Optional[str] = None  # auto-generated like {document_id}-med-{n} if omitted
    media_type: MediaType
    reference: str = Field(min_length=1)
    mime_type: Optional[str] = None
    size_bytes: Optional[int] = Field(default=None, ge=0)
    caption: Optional[str] = None
    sort_order: int = 0


class MediaOut(BaseModel):
    id: str
    documentId: Optional[str] = None
    mediaType: str
    reference: str
    mimeType: Optional[str] = None
    sizeBytes: Optional[int] = None
    caption: Optional[str] = None
    sortOrder: int


# ---------------------------------------------------------------------------
# Documents
# ---------------------------------------------------------------------------
class DocumentCreate(BaseModel):
    model_config = ConfigDict(use_enum_values=True)

    id: str = Field(min_length=1, max_length=64, pattern=r"^[A-Za-z0-9._-]+$")
    type: RecordType
    title: str = Field(min_length=1, max_length=512)
    description: str = ""
    category: str = ""
    date: str = ""
    language: str = "en"
    source: str = ""
    status: ArchivalStatus = ArchivalStatus.DRAFT
    is_sample_data: bool = False
    citation: Optional[str] = None
    event_id: Optional[str] = None
    ocr_text: Optional[str] = None
    tags: List[str] = []
    media: List[MediaCreate] = []
    related_ids: List[str] = []


class DocumentUpdate(BaseModel):
    """Partial update — only supplied fields are changed (Phase D scope:
    core fields, tags and status; media/relationships are created at POST)."""

    model_config = ConfigDict(use_enum_values=True)

    type: Optional[RecordType] = None
    title: Optional[str] = Field(default=None, min_length=1, max_length=512)
    description: Optional[str] = None
    category: Optional[str] = None
    date: Optional[str] = None
    language: Optional[str] = None
    source: Optional[str] = None
    status: Optional[ArchivalStatus] = None
    is_sample_data: Optional[bool] = None
    citation: Optional[str] = None
    event_id: Optional[str] = None
    ocr_text: Optional[str] = None
    tags: Optional[List[str]] = None


class DocumentMediaRef(BaseModel):
    id: str
    mediaType: str
    reference: str
    mimeType: Optional[str] = None
    caption: Optional[str] = None


class DocumentOut(BaseModel):
    """CamelCase contract consumed by Unity (matches ArchiveRecord JSON)."""

    model_config = ConfigDict(populate_by_name=True)

    id: str
    type: int  # Unity ArchiveRecordType numeric value
    title: str
    description: str = ""
    date: str = ""
    category: str = ""
    source: str = ""
    language: str = ""
    isSampleData: bool = False
    verified: bool = False
    status: str
    citation: Optional[str] = None
    eventId: Optional[str] = None
    ocrText: Optional[str] = None
    ocrStatus: Optional[str] = None
    ocrLanguage: Optional[str] = None
    ocrPages: int = 0
    tags: List[str] = []
    relatedIds: List[str] = []
    image: Optional[str] = None
    audio: Optional[str] = None
    video: Optional[str] = None
    document: Optional[str] = None
    pages: List[str] = []
    media: List[DocumentMediaRef] = []
    createdAt: str = ""
    updatedAt: str = ""