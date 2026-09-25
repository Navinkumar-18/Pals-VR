"""Archival document, version, metadata and media models.

Stable string primary keys are used everywhere (e.g. ``AMB-SAM-001``) so an
id survives imports/exports and matches the Unity sample dataset. Large binary
files are NOT stored here — :class:`Media` keeps typed references (paths/URIs)
to object/file storage so cloud object storage can be introduced later.
"""
from datetime import datetime, timezone
from typing import List, Optional

from sqlalchemy import (
    BigInteger,
    Boolean,
    DateTime,
    ForeignKey,
    Integer,
    JSON,
    String,
    Text,
)
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.database.base import Base
from app.models.enums import ArchivalStatus, MediaType, RecordType


def utcnow() -> datetime:
    return datetime.now(timezone.utc)


class Documents(Base):
    __tablename__ = "documents"

    id: Mapped[str] = mapped_column(String(64), primary_key=True)
    # Stored as the enum VALUE string (e.g. "MANUSCRIPT"), kept as plain
    # String so SQLAlchemy returns plain strings (no member conversion).
    type: Mapped[str] = mapped_column(String(32), index=True)
    title: Mapped[str] = mapped_column(String(512))
    description: Mapped[str] = mapped_column(Text, default="")
    category: Mapped[str] = mapped_column(String(128), default="", index=True)
    # Display date (archival dates are often partial: "c. 1891", "1916").
    date: Mapped[str] = mapped_column(String(64), default="")
    language: Mapped[str] = mapped_column(String(16), default="en")
    source: Mapped[str] = mapped_column(String(256), default="")
    status: Mapped[str] = mapped_column(
        String(16), default=ArchivalStatus.DRAFT.value, index=True
    )
    is_sample_data: Mapped[bool] = mapped_column(Boolean, default=False)
    citation: Mapped[Optional[str]] = mapped_column(Text, nullable=True)
    event_id: Mapped[Optional[str]] = mapped_column(String(64), nullable=True, index=True)
    ocr_text: Mapped[Optional[str]] = mapped_column(Text, nullable=True)
    # Document-level OCR state (Phase E). Page detail lives in ocr_results.
    ocr_status: Mapped[str] = mapped_column(String(16), default="NONE", index=True)
    ocr_language: Mapped[Optional[str]] = mapped_column(String(16), nullable=True)
    ocr_error: Mapped[Optional[str]] = mapped_column(Text, nullable=True)
    tags: Mapped[list] = mapped_column(JSON, default=list)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    updated_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow, onupdate=utcnow)

    media: Mapped[List["Media"]] = relationship(
        back_populates="document", lazy="selectin", cascade="all, delete-orphan"
    )


class DocumentVersions(Base):
    """Version history of a document's record (not its binary content)."""

    __tablename__ = "document_versions"

    id: Mapped[str] = mapped_column(String(64), primary_key=True)
    document_id: Mapped[str] = mapped_column(
        ForeignKey("documents.id", ondelete="CASCADE"), index=True
    )
    version: Mapped[int] = mapped_column(Integer, default=1)
    change_note: Mapped[Optional[str]] = mapped_column(String(512), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)


class DocumentMetadata(Base):
    """Extensible key/value metadata attached to a document."""

    __tablename__ = "document_metadata"

    id: Mapped[str] = mapped_column(String(64), primary_key=True)
    document_id: Mapped[str] = mapped_column(
        ForeignKey("documents.id", ondelete="CASCADE"), index=True
    )
    key: Mapped[str] = mapped_column(String(128))
    value: Mapped[str] = mapped_column(Text)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)


class Media(Base):
    """Typed reference to an image/audio/video/PDF/scan for a document."""

    __tablename__ = "media"

    id: Mapped[str] = mapped_column(String(64), primary_key=True)
    document_id: Mapped[Optional[str]] = mapped_column(
        ForeignKey("documents.id", ondelete="CASCADE"), nullable=True, index=True
    )
    media_type: Mapped[str] = mapped_column(String(32), index=True)
    reference: Mapped[str] = mapped_column(String(512))
    mime_type: Mapped[Optional[str]] = mapped_column(String(128), nullable=True)
    size_bytes: Mapped[Optional[int]] = mapped_column(BigInteger, nullable=True)
    caption: Mapped[Optional[str]] = mapped_column(String(512), nullable=True)
    sort_order: Mapped[int] = mapped_column(Integer, default=0)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)

    document: Mapped[Optional["Documents"]] = relationship(back_populates="media")