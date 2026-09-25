# Phase E — OCR Document Ingestion: Completion Report

**Project:** Pals-VR — Ambedkar Digital Heritage Quest
**Phase:** E — OCR-based archival document ingestion
**Status:** COMPLETE (backend + Unity integrated, verified end-to-end)
**Date:** 2026-09-25

---

## 1. What Phase E delivers

The archive now accepts scanned archival documents, OCRs them, keeps the
original scans intact, makes the extracted text full-text searchable, and lets
visitors read the transcript in VR.

```
Original scan (PDF/JPEG/PNG)
      │  POST /api/documents/upload  (multipart)
      ▼
Storage: backend/storage/originals/{doc_id}.{ext}   ← byte-for-byte, read-only
      │
      ▼  OCR worker (async daemon thread by default)
ocr_results: per-page extracted_text + confidence + status
      │
      ▼
documents.ocr_text (aggregate) + documents.ocr_status
      │
      ▼
GET /documents?q=...  (metadata + OCR-text search)
GET /documents/{id}/ocr  /  /ocr/pages/{n}  /  /original
      │
      ▼
Unity: [READ OCR TEXT] world-space viewer, PAGE x / y pagination
```

## 2. Backend (FastAPI / SQLAlchemy)

All Phase E code lives in `backend/app/`:

| Area | Files | Notes |
| --- | --- | --- |
| OCR engine abstraction | `app/ocr/base.py`, `rapid_engine.py`, `tesseract_engine.py`, `factory.py` | `OcrEngine` ABC; `rapid` (pip-only, offline, verified) default; `tesseract` code-ready for `en/hi/ta`; `OCR_ENGINE=rapid\|tesseract\|auto`; factory cached with an `override_ocr_engine` test seam |
| PDF rendering | `app/ocr/pdf_renderer.py` | PyMuPDF, dpi 200, per-page PNG |
| Text cleaning | `app/ocr/cleaner.py` | Conservative only: line-ending + NFC normalization, repeated header/footer removal (≥3 occurrences), whitespace collapse — **never rewrites** historical text |
| Persistence | `app/repositories/ocr_repository.py` | `ocr_results` rows (`{doc_id}-p{n}`), full-text `ocr_text`; every file read is containment-checked |
| Pipeline | `app/services/ocr_service.py` | Validated upload → save original → OCR (inline when `ocr_async=false`, else background thread with its own session) |
| API | `app/api/routes/documents.py` | Upload / re-OCR / status / per-page / original endpoints |

### Endpoints (all under `/api`)

| Method | Path | Purpose | Status codes covered |
| --- | --- | --- | --- |
| `POST` | `/documents/upload` | Multipart ingest (PDF/JPEG/PNG) + optional `language`/`title`/`status`; starts OCR | 201, 400, 413, 415, 422, 503 |
| `POST` | `/documents/{id}/ocr` | (Re)run OCR on a document with a stored original | 200, 404, 422, 503 |
| `GET` | `/documents/{id}/ocr` | Document status + per-page summaries | 200, 404 |
| `GET` | `/documents/{id}/ocr/pages/{n}` | Full OCR text for one page | 200, 404, 422 |
| `GET` | `/documents/{id}/original` | Download the preserved original scan | 200, 404 |

Uploaded records default to `DRAFT`; an optional admin-only `status` form field
(`PUBLISHED`, …) lets the demo publish scans so public clients (and the VR
headset) can read them.

### Data model

- `ocr_results`: stable id `{document_id}-p{page_number}`, `page_number`,
  `extracted_text`, `confidence`, `processing_status`
  (`PENDING/PROCESSING/COMPLETED/FAILED`), FK to documents with CASCADE.
- `documents` gains `ocr_status` (`NONE → PENDING → PROCESSING → COMPLETED/FAILED`),
  `ocr_language`, `ocr_error`, `ocr_text` (aggregate transcript).
- `DocumentOut` exposes camelCase `ocrStatus` / `ocrLanguage` / `ocrPages`;
  OCR DTO endpoints use `response_model_exclude_none=True` so null confidence is
  omitted (Unity `JsonUtility` tolerates missing but not null value-type fields).

### Language handling

- RapidOCR: `supported_language_codes = {"en"}` (verified live on this machine).
- Tesseract path: code-ready for `en`/`hi`/`ta` when the binary + tessdata exist
  (not installed here).
- **Never silently wrong-language**: requesting a language the selected engine
  cannot serve returns a clear 400 config error; an unavailable engine returns
  503. `require_language` messages list the human-readable available languages
  (e.g. `Available languages: English.`).

### Security

- Extension allowlist **and** magic-byte sniffing (415 wrong ext, 400 mismatched
  magic, 413 oversized via `MAX_UPLOAD_MB`).
- Server-generated filenames (`originals/{doc_id}.{ext}`); every file read is
  containment-checked against the storage root (path traversal blocked).
- Uploads are never executed; the API never returns filesystem paths.
- If the DB insert fails, the just-saved original is deleted (no orphan files).

## 3. Verification — backend

**Automated:** `43 passed` (`backend/tests/`, in-memory SQLite + fake OCR engine;
26 pre-Phase-E tests unchanged and green, 17 new Phase E tests).

New coverage: upload validation matrix, multi-page PDF OCR, per-page retrieval,
status transitions, OCR failure, missing document/page handling, original
byte-for-byte preservation, search hitting `ocr_text`, language config errors.

**Live smoke (real RapidOCR, port 8000, SQLite dev.db):**

- PNG upload → `COMPLETED`, text `"AMBEDKAR\n1891"`, confidence 0.793; original
  bytes round-tripped via `/original`; `/search?q=ambedkar` finds it.
- 3-page PDF upload → 3 rows OCR'd (`"AMBEDKAR ARCHIVE PAGE ONE/TWO/THREE"`),
  render path verified, original intact.
- `language=ta` → 400 with clear "available languages" config error.

## 4. Unity (Meta Quest / VR)

| File | Change |
| --- | --- |
| `ArchiveRecord.cs` | Backward-compatible `ocrStatus`, `ocrLanguage`, `ocrPages` fields |
| `DocumentViewer.cs` | `TextPageChars = 2200`, `SplitIntoTextPages` (line-boundary chunking + hard-split drain), counter `PAGE x / y`, sub-pages prefixed `OCR TEXT - PART n / m`; short text stays one page (backward compatible) |
| `ArchiveInfoPanel.cs` | Meta line shows `OCR: COMPLETED (n pages) [en]` when present; `[READ OCR TEXT]` wired to the text viewer |
| `MuseumRoomBuilder.cs` | Action label `READ TEXT` → `READ OCR TEXT`; button row widened (250px, font 30) |
| `ArchiveApiClient.cs` | `ApiOcrPageSummaryDto`, `ApiOcrStatusDto`, `ApiOcrPageDto` (camelCase) + runtime `GetOcrStatus`/`GetOcrPage` (UnityWebRequest) + editor sync `TryGetOcrStatusSyncEditor`/`TryGetOcrPageSyncEditor` (batch mode) |
| `FoundationRegressionChecks.cs` | 3 new checks: Phase E field parse, viewer long-text pagination, live OCR e2e (gated on `AMBEKAR_API_URL`) |

The viewer never crashes on missing OCR: absent `ocrText` still yields the
fallback "No transcript" page; null confidence is omitted server-side and
deserializes to 0 in Unity.

### Unity batch verification

`BuildAndRunAllFromCommandLine` (regenerates MainMuseum from data, then runs the
suite) against the live backend with `AMBEKAR_API_URL=http://127.0.0.1:8000/api`:

- **Regression checks: 22 passed, 0 failed** (19 pre-Phase-E + 3 new Phase E
  checks), `Exiting batchmode successfully`.
- Live API integration: 11 records served by the backend ✓
- Live OCR e2e: found published OCR-COMPLETED doc `AMB-UPL-0AC6ECE6`,
  `GET /ocr` status `COMPLETED` (1 page) + page 1 text `"AMBEDKAR 1891…"` ✓

Two earlier batch runs surfaced a test-side assertion bug (the page sentinel
counted the `OCR TEXT - PART n / m` header against the readability cap), fixed
in the check itself — the viewer code was correct. Final run is clean.

## 5. Out of scope (honestly noted)

- **PostgreSQL** not verified on this machine — SQLite used for the prototype;
  models are dialect-safe and the swap needs no code changes.
- **Quest APK build** not exercised (Android SDK/NDK toolchain unverified).
- **Tesseract** binary + `hi`/`ta` tessdata not installed; `rapid` (English) is
  the verified default. The Tesseract path is code-complete.
- Per the phase scope: no embeddings / RAG / LLM / voice / translation.

## 6. Files touched (Phase E)

- `backend/app/api/routes/documents.py`, `backend/app/services/ocr_service.py`,
  `backend/app/repositories/ocr_repository.py`, `backend/app/models/ocr.py`,
  `backend/app/models/document.py`, `backend/app/schemas/ocr.py`,
  `backend/app/schemas/document.py`, `backend/app/services/search_service.py`,
  `backend/app/ocr/*`, `backend/app/core/config.py` settings, `backend/tests/test_ocr.py`,
  `backend/requirements.txt`, `backend/.env.example`, `backend/README.md`,
  root `.gitignore` (`backend/storage/`).
- Unity: `Assets/Scripts/Interaction/DocumentViewer.cs`,
  `Assets/Scripts/Interaction/ArchiveInfoPanel.cs`,
  `Assets/Scripts/Scene/MuseumRoomBuilder.cs`,
  `Assets/Scripts/Core/ArchiveRecord.cs`,
  `Assets/Scripts/Networking/ArchiveApiClient.cs`,
  `Assets/Editor/FoundationRegressionChecks.cs`.
- Docs: this report.

All repo changes remain uncommitted on branch `mithres` (verify + commit with
the team as appropriate).