"""Engine / session factory and the FastAPI dependency.

The URL is read from settings (`DATABASE_URL`); for this phase the app is
PostgreSQL-first with a documented SQLite fallback for local prototype work
and the automated test-suite.
"""
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

from app.config.settings import get_settings

engine = create_engine(
    get_settings().database_url,
    pool_pre_ping=True,
    # SQLite has no need for a pool; this also keeps tests self-contained.
    connect_args={"check_same_thread": False}
    if get_settings().database_url.startswith("sqlite")
    else {},
)

SessionLocal = sessionmaker(bind=engine, autocommit=False, autoflush=False)


def get_db():
    """FastAPI dependency yielding a scoped session."""
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()