"""Test configuration.

Uses an in-memory SQLite database (same SQLAlchemy models as PostgreSQL) so
the suite runs without a server. Set DATABASE_URL/ADMIN_API_KEY BEFORE the
app modules are imported; tests that need a configured admin key mutate the
cached Settings object through monkeypatch.
"""
import os

os.environ["DATABASE_URL"] = "sqlite:///:memory:"
os.environ["ADMIN_API_KEY"] = ""
os.environ["AUTO_SEED"] = "false"

from app.config.settings import reset_settings  # noqa: E402

reset_settings()

import pytest  # noqa: E402
from fastapi.testclient import TestClient  # noqa: E402
from sqlalchemy import create_engine  # noqa: E402
from sqlalchemy.orm import sessionmaker  # noqa: E402
from sqlalchemy.pool import StaticPool  # noqa: E402

from app import models  # noqa: E402, F401  (register tables)
from app.database.base import Base  # noqa: E402
from app.database.session import get_db  # noqa: E402
from app.main import create_app  # noqa: E402
from app.models.document import Documents, Media  # noqa: E402
from app.models.enums import ArchivalStatus  # noqa: E402
from app.models.event import Events  # noqa: E402
from app.models.person import People  # noqa: E402
from app.models.relationship import Relationships  # noqa: E402
from app.models.topic import Topics  # noqa: E402

PUBLISHED = ArchivalStatus.PUBLISHED.value
DRAFT = ArchivalStatus.DRAFT.value

# In-memory SQLite shared across threads (TestClient uses a thread pool).
engine = create_engine(
    "sqlite://",
    connect_args={"check_same_thread": False},
    poolclass=StaticPool,
)
Base.metadata.create_all(bind=engine)
TestingSession = sessionmaker(bind=engine, autocommit=False, autoflush=False)


def override_get_db():
    db = TestingSession()
    try:
        yield db
    finally:
        db.close()


@pytest.fixture(scope="session")
def client():
    app = create_app()
    app.dependency_overrides[get_db] = override_get_db
    with TestClient(app) as c:
        yield c


@pytest.fixture()
def seeded():
    """Small published/draft dataset with an event/person/topic + links.

    Cleans all tables first (the in-memory DB is shared for the session), so
    every test starts from the same baseline.
    """
    clear_tables()
    db = TestingSession()
    db.add_all(
        [
            Documents(
                id="DOC-A-001",
                type="DOCUMENT",
                title="Sample Law Document",
                description="About constitutional law and legal frameworks.",
                category="Constitutional Work",
                date="1916",
                language="en",
                source="test-seed",
                status=PUBLISHED,
                is_sample_data=True,
                citation="test citation",
            ),
            Documents(
                id="DOC-A-002",
                type="MANUSCRIPT",
                title="Sample Manuscript",
                description="Handwritten notes on social reform.",
                category="Manuscripts",
                date="",
                language="en",
                source="test-seed",
                status=PUBLISHED,
                is_sample_data=True,
                citation="test citation",
            ),
            Documents(
                id="DOC-A-003",
                type="BOOK",
                title="Draft Private Book",
                description="Not yet released.",
                status=DRAFT,
                is_sample_data=True,
            ),
            Events(
                id="EVT-1", title="Test Event", date="1916", category="Constitutional Work"
            ),
            People(id="PER-1", full_name="Test Person"),
            Topics(id="TOP-1", name="Constitution"),
            Relationships(
                source_id="DOC-A-001", target_id="DOC-A-002", relationship_type="related"
            ),
            Relationships(
                source_id="DOC-A-001", target_id="EVT-1", relationship_type="event"
            ),
            Relationships(
                source_id="DOC-A-001", target_id="PER-1", relationship_type="person"
            ),
            Media(
                id="DOC-A-001-med-1",
                document_id="DOC-A-001",
                media_type="IMAGE",
                reference="images/doc-a-001.jpg",
                sort_order=0,
            ),
        ]
    )
    db.commit()
    db.close()
    yield


def clear_tables():
    """Empty the shared test database (used before seeding)."""
    db = TestingSession()
    for table in reversed(Base.metadata.sorted_tables):
        db.execute(table.delete())
    db.commit()
    db.close()