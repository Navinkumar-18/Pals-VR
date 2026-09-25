"""Idempotent development seed with clearly labelled [SAMPLE] records.

IDs mirror the Unity offline demo dataset (Assets/Resources/Data/
sample_archive.json) so DEMO mode and API mode resolve the same records and
exhibit IDs stay stable. No quotation or historical claim is invented — every
description is explicitly a placeholder pending institutional archival data.
"""
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.models.book import Books
from app.models.document import DocumentMetadata, DocumentVersions, Documents, Media
from app.models.enums import ArchivalStatus, MediaType, RecordType
from app.models.event import Events
from app.models.person import People
from app.models.relationship import Relationships
from app.models.speech import Speeches
from app.models.topic import Topics

PUBLISHED = ArchivalStatus.PUBLISHED.value
SAMPLE_CITATION = "Sample dataset (offline demo). Not an institutional citation."

# (media_id, media_type, reference, sort_order)
DOCUMENTS: list[dict] = [
    {
        "id": "AMB-SAM-001",
        "type": RecordType.MANUSCRIPT.value,
        "title": "[SAMPLE] Sample Manuscript Record",
        "description": "A placeholder manuscript record used for offline demo mode. Real institutional scans and OCR text will be supplied by the archive backend.",
        "category": "Manuscripts",
        "date": "",
        "tags": ["manuscript", "sample-demo"],
        "ocr_text": "[SAMPLE OCR] Placeholder transcript for the sample manuscript. The real OCR pipeline will replace this text.",
        "media": [
            ("AMB-SAM-001-med-1", MediaType.IMAGE, "images/AMB-SAM-001.jpg", 0),
            ("AMB-SAM-001-med-2", MediaType.PDF, "documents/AMB-SAM-001-scan.pdf", 0),
            ("AMB-SAM-001-med-3", MediaType.DOCUMENT_SCAN, "images/AMB-SAM-001-p1.jpg", 1),
            ("AMB-SAM-001-med-4", MediaType.DOCUMENT_SCAN, "images/AMB-SAM-001-p2.jpg", 2),
        ],
        "related": ["AMB-SAM-002"],
    },
    {
        "id": "AMB-SAM-002",
        "type": RecordType.DOCUMENT.value,
        "title": "[SAMPLE] Sample Digitized Document",
        "description": "A placeholder document whose OCR-extracted text will be searchable once the OCR pipeline is connected. This entry only proves the data path works.",
        "category": "Constitutional Work",
        "date": "1916",
        "tags": ["document", "sample-demo"],
        "ocr_text": "[SAMPLE OCR] Placeholder extracted text for the digitized document. Search and RAG will operate on this field once the OCR pipeline is live.",
        "media": [
            ("AMB-SAM-002-med-1", MediaType.PDF, "documents/AMB-SAM-002-scan.pdf", 0),
            ("AMB-SAM-002-med-2", MediaType.DOCUMENT_SCAN, "images/AMB-SAM-002-p1.jpg", 1),
            ("AMB-SAM-002-med-3", MediaType.DOCUMENT_SCAN, "images/AMB-SAM-002-p2.jpg", 2),
        ],
        "related": ["AMB-SAM-001", "AMB-SAM-005"],
    },
    {
        "id": "AMB-SAM-003",
        "type": RecordType.PHOTOGRAPH.value,
        "title": "[SAMPLE] Sample Photograph Record",
        "description": "Placeholder record for a scanned photograph. The archive stores the original file, metadata, and a stable document id.",
        "category": "Photographs",
        "date": "",
        "tags": ["photograph", "sample-demo"],
        "media": [("AMB-SAM-003-med-1", MediaType.IMAGE, "images/AMB-SAM-003.jpg", 0)],
        "related": [],
    },
    {
        "id": "AMB-SAM-004",
        "type": RecordType.SPEECH.value,
        "title": "[SAMPLE] Sample Speech Record",
        "description": "Placeholder record for an audio speech. Audio narration and text-to-speech are wired in later phases.",
        "category": "Speeches",
        "date": "1942",
        "tags": ["speech", "audio", "sample-demo"],
        "ocr_text": "[SAMPLE] Placeholder speech transcript text.",
        "media": [
            ("AMB-SAM-004-med-1", MediaType.AUDIO, "audio/AMB-SAM-004.mp3", 0),
            ("AMB-SAM-004-med-2", MediaType.PDF, "documents/AMB-SAM-004-transcript.pdf", 0),
        ],
        "related": ["AMB-SAM-005"],
    },
    {
        "id": "AMB-SAM-005",
        "type": RecordType.EVENT.value,
        "title": "[SAMPLE] Sample Timeline Event",
        "description": "Placeholder timeline event demonstrating how the 3D timeline will load configurable events from data instead of being hard-coded.",
        "category": "Life & Journey",
        "date": "1891",
        "event_id": "TL-1891",
        "tags": ["timeline", "event", "sample-demo"],
        "media": [],
        "related": ["AMB-SAM-002", "AMB-SAM-004"],
    },
    {
        "id": "AMB-SAM-006",
        "type": RecordType.BOOK.value,
        "title": "[SAMPLE] Sample Book Record",
        "description": "Placeholder record for a book volume in the collection. Demonstrates the extended multimedia, OCR and citation fields.",
        "category": "Books",
        "date": "",
        "tags": ["book", "publication", "sample-demo"],
        "ocr_text": "[SAMPLE OCR] Placeholder extracted text from the sample book volume.",
        "media": [
            ("AMB-SAM-006-med-1", MediaType.IMAGE, "images/AMB-SAM-006.jpg", 0),
            ("AMB-SAM-006-med-2", MediaType.PDF, "documents/AMB-SAM-006-volume.pdf", 0),
            ("AMB-SAM-006-med-3", MediaType.DOCUMENT_SCAN, "images/AMB-SAM-006-p1.jpg", 1),
            ("AMB-SAM-006-med-4", MediaType.DOCUMENT_SCAN, "images/AMB-SAM-006-p2.jpg", 2),
            ("AMB-SAM-006-med-5", MediaType.DOCUMENT_SCAN, "images/AMB-SAM-006-p3.jpg", 3),
        ],
        "related": ["AMB-SAM-002"],
    },
    {
        "id": "AMB-SAM-007",
        "type": RecordType.LETTER.value,
        "title": "[SAMPLE] Sample Letter Record",
        "description": "Placeholder record for a piece of correspondence. Demonstrates records without full multimedia coverage.",
        "category": "Correspondence",
        "date": "",
        "tags": ["letter", "correspondence", "sample-demo"],
        "ocr_text": "[SAMPLE OCR] Placeholder transcript of the sample letter.",
        "media": [
            ("AMB-SAM-007-med-1", MediaType.PDF, "documents/AMB-SAM-007-letter.pdf", 0),
            ("AMB-SAM-007-med-2", MediaType.DOCUMENT_SCAN, "images/AMB-SAM-007-p1.jpg", 1),
        ],
        "related": ["AMB-SAM-002", "AMB-SAM-005"],
    },
    {
        "id": "AMB-SAM-008",
        "type": RecordType.VIDEO.value,
        "title": "[SAMPLE] Sample Video Record",
        "description": "Placeholder record for a video archive item. Playback will stream from the archive backend in a later phase.",
        "category": "Video Archive",
        "date": "",
        "tags": ["video", "sample-demo"],
        "media": [("AMB-SAM-008-med-1", MediaType.VIDEO, "video/AMB-SAM-008.mp4", 0)],
        "related": ["AMB-SAM-004"],
    },
]

EVENTS = [
    {
        "id": "TL-1891",
        "title": "[SAMPLE] Timeline: 1891 (Sample Event)",
        "description": "Placeholder timeline event. Date intentionally shows the uncatalogued/undated handling.",
        "date": "1891",
        "category": "Life & Journey",
    },
    {
        "id": "TL-1916",
        "title": "[SAMPLE] Timeline: 1916 (Sample Event)",
        "description": "Placeholder timeline event linked to the sample digitized document dated 1916.",
        "date": "1916",
        "category": "Constitutional Work",
    },
    {
        "id": "TL-1942",
        "title": "[SAMPLE] Timeline: 1942 (Sample Event)",
        "description": "Placeholder timeline event linked to the sample speech record dated 1942.",
        "date": "1942",
        "category": "Speeches",
    },
]

PEOPLE = [
    {
        "id": "AMB-PER-001",
        "full_name": "[SAMPLE] B.R. Ambedkar (placeholder person record)",
        "description": "Placeholder person entry. Biographical data pending institutional archival verification.",
    }
]

TOPICS = [
    ("AMB-TOP-001", "[SAMPLE] Constitution"),
    ("AMB-TOP-002", "[SAMPLE] Law & Legal Studies"),
    ("AMB-TOP-003", "[SAMPLE] Social Reform"),
]

RELATIONSHIPS: list[tuple[str, str, str]] = [
    # related-document links (mirror relatedIds in the Unity demo)
    ("AMB-SAM-001", "AMB-SAM-002", "related"),
    ("AMB-SAM-002", "AMB-SAM-001", "related"),
    ("AMB-SAM-002", "AMB-SAM-005", "related"),
    ("AMB-SAM-004", "AMB-SAM-005", "related"),
    ("AMB-SAM-005", "AMB-SAM-002", "related"),
    ("AMB-SAM-005", "AMB-SAM-004", "related"),
    ("AMB-SAM-006", "AMB-SAM-002", "related"),
    ("AMB-SAM-007", "AMB-SAM-002", "related"),
    ("AMB-SAM-007", "AMB-SAM-005", "related"),
    ("AMB-SAM-008", "AMB-SAM-004", "related"),
    # typed relationships
    ("AMB-SAM-005", "TL-1891", "event"),
    ("AMB-SAM-002", "TL-1916", "event"),
    ("AMB-SAM-004", "TL-1942", "event"),
    ("AMB-SAM-004", "AMB-PER-001", "speaker"),
    ("AMB-SAM-006", "AMB-PER-001", "author_of"),
    ("AMB-SAM-001", "AMB-TOP-001", "topic"),
    ("AMB-SAM-002", "AMB-TOP-002", "topic"),
    ("AMB-SAM-005", "AMB-TOP-001", "topic"),
]

COMMON_FIELDS = {
    "language": "en",
    "source": "sample-dataset",
    "status": PUBLISHED,
    "is_sample_data": True,
    "citation": SAMPLE_CITATION,
}


def _ids(db: Session, model) -> set[str]:
    return set(db.execute(select(model.id)).scalars().all())


def seed_demo_data(db: Session) -> int:
    """Insert any missing [SAMPLE] records. Returns number of documents added."""
    created = 0

    doc_ids = _ids(db, Documents)
    for spec in DOCUMENTS:
        if spec["id"] in doc_ids:
            continue
        db.add(
            Documents(
                id=spec["id"],
                type=spec["type"],
                title=spec["title"],
                description=spec["description"],
                category=spec["category"],
                date=spec.get("date", ""),
                language=COMMON_FIELDS["language"],
                source=COMMON_FIELDS["source"],
                status=COMMON_FIELDS["status"],
                is_sample_data=True,
                citation=COMMON_FIELDS["citation"],
                event_id=spec.get("event_id"),
                ocr_text=spec.get("ocr_text"),
                tags=spec.get("tags", []),
            )
        )
        for media_id, mtype, reference, sort_order in spec["media"]:
            db.add(
                Media(
                    id=media_id,
                    document_id=spec["id"],
                    media_type=mtype.value,
                    reference=reference,
                    sort_order=sort_order,
                )
            )
        created += 1
    db.flush()

    existing_rels = set(db.execute(select(Relationships.source_id, Relationships.target_id, Relationships.relationship_type)).all())
    for source, target, rel_type in RELATIONSHIPS:
        if (source, target, rel_type) not in existing_rels:
            db.add(
                Relationships(
                    source_id=source, target_id=target, relationship_type=rel_type
                )
            )

    for spec in EVENTS:
        if spec["id"] not in _ids(db, Events):
            db.add(Events(**spec))

    for spec in PEOPLE:
        if spec["id"] not in _ids(db, People):
            db.add(People(**spec))

    topic_ids = _ids(db, Topics)
    for tid, name in TOPICS:
        if tid not in topic_ids:
            db.add(Topics(id=tid, name=name, description="Placeholder sample topic."))

    if "AMB-SPC-001" not in _ids(db, Speeches):
        db.add(
            Speeches(
                id="AMB-SPC-001",
                title="[SAMPLE] Speech link placeholder",
                document_id="AMB-SAM-004",
                event_id="TL-1942",
                speaker_id="AMB-PER-001",
                date="1942",
            )
        )

    if "AMB-BOK-001" not in _ids(db, Books):
        db.add(
            Books(
                id="AMB-BOK-001",
                title="[SAMPLE] Book volume link placeholder",
                document_id="AMB-SAM-006",
                author_id="AMB-PER-001",
            )
        )

    version_ids = _ids(db, DocumentVersions)
    for doc_id in ("AMB-SAM-001", "AMB-SAM-002", "AMB-SAM-006"):
        vid = f"{doc_id}-v1"
        if vid not in version_ids:
            db.add(
                DocumentVersions(
                    id=vid, document_id=doc_id, version=1,
                    change_note="Initial [SAMPLE] import",
                )
            )

    meta_ids = _ids(db, DocumentMetadata)
    if "AMB-MD-001" not in meta_ids:
        db.add(DocumentMetadata(id="AMB-MD-001", document_id="AMB-SAM-001", key="physical_format", value="paper"))
        db.add(DocumentMetadata(id="AMB-MD-002", document_id="AMB-SAM-002", key="scan_pipeline", value="pending (Phase F)"))

    db.commit()
    return created