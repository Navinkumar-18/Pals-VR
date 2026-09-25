"""People (persons of interest in the archive)."""
from datetime import datetime
from typing import Optional

from sqlalchemy import DateTime, Integer, String, Text
from sqlalchemy.orm import Mapped, mapped_column

from app.database.base import Base
from app.models.document import utcnow


class People(Base):
    __tablename__ = "people"

    id: Mapped[str] = mapped_column(String(64), primary_key=True)
    full_name: Mapped[str] = mapped_column(String(256))
    birth_year: Mapped[Optional[int]] = mapped_column(Integer, nullable=True)
    death_year: Mapped[Optional[int]] = mapped_column(Integer, nullable=True)
    description: Mapped[str] = mapped_column(Text, default="")
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    updated_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow, onupdate=utcnow)