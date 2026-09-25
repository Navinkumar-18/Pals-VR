"""Flexible entity-agnostic relationships.

``source_id``/``target_id`` reference any entity (document, event, person,
topic, speech, book) by its stable id; ``relationship_type`` is an open string
("related", "event", "person", "topic", "speaker", "author_of", ...). No hard
foreign keys are enforced so the model stays flexible for Phase L (knowledge
mapping); integrity is the responsibility of the seed/service layer.
"""
from datetime import datetime
from typing import Optional

from sqlalchemy import DateTime, Integer, String
from sqlalchemy.orm import Mapped, mapped_column

from app.database.base import Base
from app.models.document import utcnow


class Relationships(Base):
    __tablename__ = "relationships"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    source_id: Mapped[str] = mapped_column(String(64), index=True)
    target_id: Mapped[str] = mapped_column(String(64), index=True)
    relationship_type: Mapped[str] = mapped_column(String(64), index=True)
    notes: Mapped[Optional[str]] = mapped_column(String(512), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)