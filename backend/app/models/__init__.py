"""Import every model so ``Base.metadata`` knows all tables (create_all)."""
from app.models.book import Books
from app.models.document import DocumentMetadata, DocumentVersions, Documents, Media
from app.models.enums import ArchivalStatus, MediaType, RecordType
from app.models.event import Events
from app.models.ocr import OcrResultRow
from app.models.person import People
from app.models.relationship import Relationships
from app.models.speech import Speeches
from app.models.topic import Topics

__all__ = [
    "ArchivalStatus",
    "Books",
    "DocumentMetadata",
    "DocumentVersions",
    "Documents",
    "Events",
    "Media",
    "MediaType",
    "OcrResultRow",
    "People",
    "RecordType",
    "Relationships",
    "Speeches",
    "Topics",
]