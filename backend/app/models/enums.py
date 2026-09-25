"""Domain enums for the archive data model.

Values are stored as stable uppercase strings (safe on PostgreSQL and SQLite)
and serialized as strings in the JSON API, except ``RecordType`` which is
serialized as its NUMERIC value to match the Unity ``ArchiveRecordType``
contract (1=Manuscript ... 9=Event).
"""
from enum import StrEnum


class RecordType(StrEnum):
    MANUSCRIPT = "MANUSCRIPT"
    BOOK = "BOOK"
    PHOTOGRAPH = "PHOTOGRAPH"
    LETTER = "LETTER"
    DOCUMENT = "DOCUMENT"
    SPEECH = "SPEECH"
    AUDIO = "AUDIO"
    VIDEO = "VIDEO"
    EVENT = "EVENT"

    @property
    def unity_value(self) -> int:
        """Numeric value used by Unity's ArchiveRecordType enum in JSON."""
        return {
            RecordType.MANUSCRIPT: 1,
            RecordType.BOOK: 2,
            RecordType.PHOTOGRAPH: 3,
            RecordType.LETTER: 4,
            RecordType.DOCUMENT: 5,
            RecordType.SPEECH: 6,
            RecordType.AUDIO: 7,
            RecordType.VIDEO: 8,
            RecordType.EVENT: 9,
        }[self]


class ArchivalStatus(StrEnum):
    """Archival/verification lifecycle of a record.

    Public clients (the Unity VR app) only ever receive PUBLISHED records.
    """

    DRAFT = "DRAFT"
    REVIEW = "REVIEW"
    VERIFIED = "VERIFIED"
    PUBLISHED = "PUBLISHED"
    ARCHIVED = "ARCHIVED"


class MediaType(StrEnum):
    IMAGE = "IMAGE"
    AUDIO = "AUDIO"
    VIDEO = "VIDEO"
    PDF = "PDF"
    DOCUMENT_SCAN = "DOCUMENT_SCAN"