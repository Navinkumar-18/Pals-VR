"""Phase E: OCR document ingestion tests.

Uses a fake OcrEngine (deterministic, no tesseract/rapid download) injected
through the public factory seam, an in-memory SQLite DB, and a per-test temp
storage root. `OCR_ASYNC` is forced to false so uploads process inline.
"""
import io

import pytest
from PIL import Image

from app.ocr.base import OcrEngine, OcrError, OcrPageResult

PUBLISHED = "PUBLISHED"
DRAFT = "DRAFT"


# ---------------------------------------------------------------------------
# Fakes
# ---------------------------------------------------------------------------
class FakeOcrEngine(OcrEngine):
    """Configurable engine: per-page texts, optional failure, language set."""

    name = "fake"

    def __init__(self, texts=None, fail=False, languages=None):
        self._texts = texts or ["Hello from the archive.", "Second page content."]
        self._fail = fail
        self._languages = languages or {"en"}

    @property
    def supported_language_codes(self):
        return set(self._languages)

    def ocr_image(self, image_bytes, language="en"):
        normalized = self.require_language(language)
        if self._fail:
            raise OcrError("fake engine image failure")
        text = self._texts[0] if self._texts else ""
        return OcrPageResult(
            page_number=1, text=text, language=normalized,
            confidence=0.95, engine=self.name,
        )

    def ocr_pdf(self, pdf_bytes, language="en"):
        normalized = self.require_language(language)
        if self._fail:
            raise OcrError("fake engine pdf failure")
        return [
            OcrPageResult(
                page_number=i, text=t, language=normalized,
                confidence=0.9, engine=self.name,
            )
            for i, t in enumerate(self._texts, start=1)
        ]


def _png_bytes(text="Hello from the archive."):
    """A valid PNG (magic bytes pass validation); content is opaque to fakes."""
    buf = io.BytesIO()
    Image.new("RGB", (320, 120), "white").save(buf, format="PNG")
    return buf.getvalue()


def _jpeg_bytes():
    buf = io.BytesIO()
    Image.new("RGB", (320, 120), "white").save(buf, format="JPEG")
    return buf.getvalue()


def _pdf_bytes(page_texts):
    """A valid multi-page PDF via PyMuPDF (no external poppler needed)."""
    import pymupdf

    doc = pymupdf.open()
    for text in page_texts:
        page = doc.new_page(width=400, height=300)
        page.insert_text((40, 90), text, fontsize=14)
    return doc.tobytes()


def _upload(client, filename, content, mime, **form):
    return client.post(
        "/api/documents/upload",
        files={"file": (filename, content, mime)},
        data=form,
    )


@pytest.fixture()
def fake_ocr(monkeypatch, tmp_path):
    """Inline OCR + temp storage + a fresh fake engine for each test."""
    from app.config.settings import get_settings
    from app.ocr import clear_ocr_engine_override, override_ocr_engine

    monkeypatch.setattr(get_settings(), "ocr_async", False)
    monkeypatch.setattr(get_settings(), "storage_root", str(tmp_path))
    engine = FakeOcrEngine()
    override_ocr_engine(engine)
    yield engine
    clear_ocr_engine_override()


# ---------------------------------------------------------------------------
# Upload pipeline
# ---------------------------------------------------------------------------
def test_upload_png_runs_ocr_and_marks_completed(client, seeded, fake_ocr):
    content = _png_bytes()
    r = _upload(client, "scan.png", content, "image/png",
                language="en", title="My Scan", status=PUBLISHED)
    assert r.status_code == 201
    body = r.json()
    doc_id = body["document"]["id"]
    assert doc_id.startswith("AMB-UPL-")
    assert body["document"]["status"] == PUBLISHED
    assert body["document"]["ocrStatus"] == "COMPLETED"
    assert body["document"]["ocrLanguage"] == "en"
    assert body["document"]["ocrPages"] == 1
    assert "Hello from the archive." in body["document"]["ocrText"]
    assert body["ocr"]["status"] == "COMPLETED"
    assert body["ocr"]["pages"] == 1
    assert len(body["ocr"]["pageSummaries"]) == 1
    assert body["ocr"]["pageSummaries"][0]["pageNumber"] == 1
    # The document endpoint exposes the same OCR state (Unity contract).
    fetched = client.get(f"/api/documents/{doc_id}")
    assert fetched.json()["ocrStatus"] == "COMPLETED"


def test_upload_pdf_multipage_ocr(client, seeded, fake_ocr):
    page_texts = ["First page words.", "Second page words.", "Third page words."]
    fake_ocr._texts = page_texts
    pdf = _pdf_bytes(page_texts)
    r = _upload(client, "book.pdf", pdf, "application/pdf", status=PUBLISHED)
    assert r.status_code == 201
    body = r.json()
    doc_id = body["document"]["id"]
    assert body["document"]["ocrPages"] == 3
    assert body["ocr"]["pages"] == 3
    status = client.get(f"/api/documents/{doc_id}/ocr").json()
    assert status["status"] == "COMPLETED"
    assert [p["pageNumber"] for p in status["pageSummaries"]] == [1, 2, 3]

    first = client.get(f"/api/documents/{doc_id}/ocr/pages/1").json()
    assert first["status"] == "COMPLETED"
    assert first["extractedText"] == "First page words."
    assert first["confidence"] is not None
    third = client.get(f"/api/documents/{doc_id}/ocr/pages/3").json()
    assert "Third page words." in third["extractedText"]
    # aggregate transcript keeps page boundaries visible (Phase F friendly)
    assert "First page words." in body["document"]["ocrText"]
    assert "\n\n" in body["document"]["ocrText"]


def test_upload_jpeg_single_page(client, seeded, fake_ocr):
    r = _upload(client, "photo.jpg", _jpeg_bytes(), "image/jpeg", status=PUBLISHED)
    assert r.status_code == 201
    body = r.json()
    assert body["document"]["ocrPages"] == 1
    assert body["ocr"]["status"] == "COMPLETED"


def test_upload_rejects_unsupported_extension(client, seeded, fake_ocr):
    r = _upload(client, "notes.txt", b"some plain text", "text/plain")
    assert r.status_code == 415
    assert "Unsupported" in r.json()["detail"]


def test_upload_requires_a_file(client, seeded, fake_ocr):
    # No file part at all -> FastAPI multipart validation rejects it (422).
    r = client.post("/api/documents/upload", data={})
    assert r.status_code == 422


def test_upload_rejects_corrupted_content(client, seeded, fake_ocr):
    # .png extension but PNG magic bytes missing -> renamed/corrupted file.
    r = _upload(client, "scan.png", b"not a png at all", "image/png")
    assert r.status_code == 400
    assert "does not match" in r.json()["detail"]


def test_upload_rejects_oversized(client, seeded, fake_ocr, monkeypatch):
    from app.config.settings import get_settings

    monkeypatch.setattr(get_settings(), "max_upload_mb", 1)
    big = b"x" * (2 * 1024 * 1024)
    r = _upload(client, "big.png", big, "image/png")
    assert r.status_code == 413
    assert "limit" in r.json()["detail"]


def test_upload_rejects_unavailable_language(client, seeded, fake_ocr):
    fake_ocr._languages = {"en"}  # only English available
    r = _upload(client, "scan.png", _png_bytes(), "image/png", language="hi")
    assert r.status_code == 400
    assert "language" in r.json()["detail"].lower()
    assert "English" in r.json()["detail"]


def test_upload_returns_503_when_no_engine(client, seeded, monkeypatch):
    from app.ocr.base import OcrEngineUnavailableError
    import app.services.ocr_service as svc

    def no_engine():
        raise OcrEngineUnavailableError("No OCR engine available at all.")

    monkeypatch.setattr(svc, "get_ocr_engine", no_engine)
    r = _upload(client, "scan.png", _png_bytes(), "image/png")
    assert r.status_code == 503
    assert "engine" in r.json()["detail"].lower()


def test_original_is_preserved_and_downloadable(client, seeded, fake_ocr, tmp_path):
    content = _png_bytes()
    r = _upload(client, "scan.png", content, "image/png", status=PUBLISHED)
    doc_id = r.json()["document"]["id"]

    original = client.get(f"/api/documents/{doc_id}/original")
    assert original.status_code == 200
    assert original.content == content  # byte-for-byte preserved
    assert original.headers["content-type"].startswith("image/png")

    # the file exists on disk under storage_root/originals, untouched by OCR
    files = list((tmp_path / "originals").iterdir())
    assert len(files) == 1
    assert files[0].read_bytes() == content


def test_original_missing_returns_422(client, seeded, fake_ocr):
    # DOC-A-001 is PUBLISHED but references an image that does not exist on
    # this storage root -> a clear "original not available" error.
    r = client.get("/api/documents/DOC-A-001/original")
    assert r.status_code == 422
    assert "original" in r.json()["detail"].lower()


def test_ocr_failure_marks_document_failed(client, seeded, fake_ocr):
    fake_ocr._fail = True
    r = _upload(client, "broken.pdf", _pdf_bytes(["x"]), "application/pdf", status=PUBLISHED)
    assert r.status_code == 201
    body = r.json()
    doc_id = body["document"]["id"]
    assert body["ocr"]["status"] == "FAILED"
    assert body["ocr"]["error"]

    status = client.get(f"/api/documents/{doc_id}/ocr").json()
    assert status["status"] == "FAILED"
    assert status["pages"] == 0
    # Document serialization carries the failure too.
    assert client.get(f"/api/documents/{doc_id}").json()["ocrStatus"] == "FAILED"


def test_missing_document_or_page_returns_404(client, seeded, fake_ocr):
    assert client.get("/api/documents/NOPE/ocr").status_code == 404
    assert client.get("/api/documents/NOPE/original").status_code == 404
    # valid doc, no OCR pages yet
    r = _upload(client, "scan.png", _png_bytes(), "image/png", status=DRAFT)
    doc_id = r.json()["document"]["id"]
    assert client.get(f"/api/documents/{doc_id}/ocr/pages/1").status_code == 404


# ---------------------------------------------------------------------------
# Access gating + status lifecycle
# ---------------------------------------------------------------------------
def test_non_published_ocr_reads_require_admin(client, seeded, fake_ocr, monkeypatch):
    from app.config.settings import get_settings

    r = _upload(client, "scan.png", _png_bytes(), "image/png", status=DRAFT)
    doc_id = r.json()["document"]["id"]

    monkeypatch.setattr(get_settings(), "admin_api_key", "test-secret")
    try:
        # public clients cannot see OCR state of a DRAFT document
        assert client.get(f"/api/documents/{doc_id}/ocr").status_code == 404
        assert client.get(f"/api/documents/{doc_id}/ocr/pages/1").status_code == 404
        assert client.get(f"/api/documents/{doc_id}/original").status_code == 404
        # with the key they can
        headers = {"X-API-Key": "test-secret"}
        assert client.get(f"/api/documents/{doc_id}/ocr", headers=headers).status_code == 200
        assert client.get(f"/api/documents/{doc_id}/ocr/pages/1", headers=headers).status_code == 200
        assert client.get(f"/api/documents/{doc_id}/original", headers=headers).status_code == 200
    finally:
        monkeypatch.setattr(get_settings(), "admin_api_key", "")


def test_post_ocr_rerun(client, seeded, fake_ocr):
    r = _upload(client, "scan.png", _png_bytes(), "image/png", status=PUBLISHED)
    doc_id = r.json()["document"]["id"]
    rerun = client.post(f"/api/documents/{doc_id}/ocr")
    assert rerun.status_code == 200
    assert rerun.json()["status"] == "COMPLETED"
    assert rerun.json()["pages"] == 1
    # OCR failure path via the trigger endpoint too
    fake_ocr._fail = True
    failed = client.post(f"/api/documents/{doc_id}/ocr")
    assert failed.status_code == 200
    assert failed.json()["status"] == "FAILED"
    assert client.post("/api/documents/NOPE/ocr").status_code == 404


def test_search_matches_ocr_text(client, seeded, fake_ocr):
    fake_ocr._texts = ["AMBEDKAR HERITAGE UNIQUE PHRASE", "More words."]
    r = _upload(client, "book.pdf", _pdf_bytes(fake_ocr._texts), "application/pdf",
                title="Ingested scan", status=PUBLISHED)
    assert r.status_code == 201
    doc_id = r.json()["document"]["id"]

    res = client.get("/api/search", params={"q": "UNIQUE PHRASE"})
    assert res.status_code == 200
    ids = [d["id"] for d in res.json()["items"]]
    assert doc_id in ids  # the archive is full-text searchable via ocr_text


def test_upload_response_includes_ocr_contract(client, seeded, fake_ocr):
    r = _upload(client, "scan.png", _png_bytes(), "image/png", status=PUBLISHED)
    body = r.json()
    doc = body["document"]
    # Unity ArchiveRecord camelCase contract (Phase E fields)
    assert "ocrStatus" in doc
    assert "ocrLanguage" in doc
    assert "ocrPages" in doc
    assert "ocrText" in doc
    assert body["ocr"]["pageSummaries"][0]["pageNumber"] == 1