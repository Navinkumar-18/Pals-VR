# Ambedkar Digital Heritage — Backend (Phases D–E)

REST API + data layer for the Ambedkar Digital Heritage VR platform.

```
META QUEST
    |
    v
UNITY VR
    |
 HTTPS/REST
    |
    v
FASTAPI BACKEND
    |
    +----------------+
    |                |
    v                v
POSTGRESQL       FILE STORAGE
```

- **FastAPI** serves the REST API under `/api` (OpenAPI docs at `/docs`, schema
  at `/openapi.json`).
- **SQLAlchemy 2.x ORM** talks to **PostgreSQL** (default). For local prototype
  and the automated tests a **SQLite** URL is accepted via `DATABASE_URL`; the
  models are dialect-safe so switching to PostgreSQL needs no code changes.
- **File storage**: `media.reference` stores a path/URI — binaries are **never**
  written into PostgreSQL. Local file layout works today; an object-storage
  driver can be swapped in later without schema changes.
- Unity never connects to PostgreSQL directly — it only talks to this API.

> Phase D scope: REST foundation, data model, seed data, offline fallback.
> Phase E adds OCR document ingestion (upload -> OCR -> cleaned per-page text
> -> searchable archive -> readable in VR). Embeddings / RAG / LLM / STT / TTS /
> translation are **later phases**.

---

## 1. Installation

Requires **Python 3.11+** (developed on 3.13).

```bash
cd backend
python -m venv .venv

# Windows
.venv\Scripts\activate
# macOS / Linux
source .venv/bin/activate

pip install -r requirements.txt
```

## 2. PostgreSQL setup (production default)

Once only, create the role and database:

```sql
CREATE ROLE archive LOGIN PASSWORD 'archive';
CREATE DATABASE ambedkar_heritage OWNER archive;
```

Windows: install PostgreSQL (e.g. `winget install PostgreSQL.PostgreSQL.16` /
EDB installer), add `psql` to PATH, then run the two statements above.

No PostgreSQL installed? For local prototype work only you can skip this step
and use SQLite (see below).

## 3. Environment variables

Copy `.env.example` to `.env` and edit. NEVER commit `.env`.

| Variable          | Default                                                            | Meaning                                            |
| ----------------- | ------------------------------------------------------------------ | -------------------------------------------------- |
| `ENVIRONMENT`     | `development`                                                      | Runtime label (`/api/health`)                      |
| `DATABASE_URL`    | `postgresql+psycopg://archive:archive@localhost:5432/ambedkar_heritage` | SQLAlchemy URL. Use `sqlite:///./dev.db` for prototype only |
| `ADMIN_API_KEY`   | *(empty)*                                                          | Shared secret for POST/PUT/DELETE + non-published reads. Empty = dev mode (open) |
| `AUTO_SEED`       | `true`                                                             | Create tables + seed `[SAMPLE]` data when DB empty |
| `CORS_ORIGINS`    | `*`                                                                | Comma-separated origins (native VR clients ignore CORS) |
| `STORAGE_ROOT`    | `storage`                                                          | Folder for uploaded original scans (relative to backend cwd) |
| `MAX_UPLOAD_MB`   | `25`                                                               | Max accepted upload size in MB                     |
| `OCR_ENGINE`      | `rapid`                                                            | `rapid` (pip-only, English) \| `tesseract` (en/hi/ta, needs binary) \| `auto` |
| `OCR_DEFAULT_LANGUAGE` | `en`                                                          | Default OCR language; unsupported -> clear config error |
| `OCR_ASYNC`       | `true`                                                             | Background-thread OCR (`false` = inline, used by tests) |

## 4. Initialize + seed

Tables are created automatically on startup (and `AUTO_SEED=true` inserts the
`[SAMPLE]` dataset). To run it explicitly:

```bash
python scripts/init_db.py
```

The seed is idempotent and mirrors the Unity offline dataset
(`Assets/Resources/Data/sample_archive.json`) — the same stable
`AMB-SAM-001` … `AMB-SAM-008` ids — so DEMO mode and API mode resolve the same
records. Every seeded row is flagged `is_sample_data=true`; descriptions say
"placeholder" and no quotations/historical claims are invented.

## 5. Run the API

```bash
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

- API root: `http://127.0.0.1:8000/api`
- OpenAPI docs: `http://127.0.0.1:8000/docs`
- Health: `http://127.0.0.1:8000/api/health`

## 6. API endpoints

All under `/api`. Public clients only ever receive **PUBLISHED** documents.

| Method | Path                    | Description                                     | Access  |
| ------ | ----------------------- | ----------------------------------------------- | ------- |
| GET    | `/health`               | Liveness + DB status                            | public  |
| GET    | `/documents`            | List (status filter defaults PUBLISHED)         | public  |
| GET    | `/documents/{id}`       | One document                                    | public  |
| POST   | `/documents`            | Create (with optional `media`, `related_ids`)   | admin   |
| PUT    | `/documents/{id}`       | Update core fields / tags / status              | admin   |
| DELETE | `/documents/{id}`       | Delete (also cleans its relationships)          | admin   |
| GET    | `/events` `/events/{id}`| Timeline events                                 | public  |
| GET    | `/media`                | Media metadata (`media_type`, `document_id` filters) | public |
| GET    | `/people`               | People                                          | public  |
| GET    | `/topics`               | Topics                                          | public  |
| GET    | `/related/{id}`         | Related documents with relationship types       | public  |
| GET    | `/search`               | Metadata + OCR-text search (`q`, `type`, `category`, `language`) | public |
| POST   | `/documents/upload`     | Ingest archival scan (multipart file: PDF/JPEG/PNG) + start OCR | admin |
| POST   | `/documents/{id}/ocr`   | (Re)run OCR on a document with a stored original | admin |
| GET    | `/documents/{id}/ocr`   | OCR status + per-page summaries                 | public\* |
| GET    | `/documents/{id}/ocr/pages/{n}` | Full OCR text for one page              | public\* |
| GET    | `/documents/{id}/original` | Download the preserved original scan        | public\* |

\* Only for **PUBLISHED** documents unless the admin key is supplied (described
below). `POST /documents/upload` accepts optional form fields `language`
(ISO 639-1, defaults to `OCR_DEFAULT_LANGUAGE`), `title`, and `status`
(default `DRAFT`). Uploaded records keep their original byte-for-byte on disk —
OCR output is derived text stored separately (see `ocr_results`).

List endpoints return a wrapper: `{"items": [...], "count", "limit", "offset"}`.
Documents serialize with **camelCase** keys and a **numeric `type`**, matching
Unity's `ArchiveRecord` exactly, so Unity parses them with `JsonUtility`
directly.

**Statuses** (`documents.status`): `DRAFT`, `REVIEW`, `VERIFIED`, `PUBLISHED`,
`ARCHIVED`. Only `PUBLISHED` is served to public clients; other statuses (both
list filtering and single reads) require `X-API-Key: <ADMIN_API_KEY>`.

**Example admin create:**

```bash
curl -X POST http://127.0.0.1:8000/api/documents \
  -H "Content-Type: application/json" -H "X-API-Key: your-key" \
  -d '{
    "id": "AMB-REAL-001",
    "type": "MANUSCRIPT",
    "title": "Real manuscript (not sample)",
    "status": "DRAFT",
    "media": [{"media_type": "PDF", "reference": "documents/AMB-REAL-001.pdf"}]
  }'
```

## 7. Security notes

- `ADMIN_API_KEY` guards writes and non-published reads via the `X-API-Key`
  header. If it is empty the API runs **open in dev mode** and logs a warning —
  set a key before exposing it publicly.
- Input is validated (Pydantic): IDs must match `[A-Za-z0-9._-]+`, empty titles
  are rejected, unknown enums are rejected (422).
- Uploads are validated defensively: extension allowlist (`.pdf/.jpg/.jpeg/.png`)
  **and** magic-byte sniffing, plus a size cap (`MAX_UPLOAD_MB`). Uploads are
  never executed and are stored under `STORAGE_ROOT` with **server-generated
  filenames**; every file read is containment-checked against the storage root
  (path-traversal blocked). The API never returns filesystem paths.
- OCR never silently uses the wrong language: requesting an unavailable
  language returns a clear configuration error (400 / 503).
- No credentials, keys or tokens are stored in the repo. `.env` is gitignored.
- Authentication here is intentionally minimal — advanced auth is a later phase.

## 8. Unity connection configuration

In the Unity project the backend is optional:

| Setting              | Where                                               | Effect                         |
| -------------------- | --------------------------------------------------- | ------------------------------ |
| `MuseumApp.IsDemoMode` | `Assets/Scripts/Core/MuseumApp.cs` (default `true`) | `true` = local sample JSON only |
| `MuseumApp.ApiBaseUrl` | same file (default `http://127.0.0.1:8000/api`)   | base URL for `ArchiveApiClient` |

Switch `IsDemoMode` to `false` (or drive it from build config) to run against
the API. `ArchiveService` then fetches PUBLISHED documents, and if the backend
is unreachable it **automatically falls back to the local demo dataset** and
flags `ArchiveService.IsOfflineFallback` (the VR status readout shows
`Data: OFFLINE-DEMO`) — the app never crashes and exhibit interaction code is
unchanged.

For a headset on another machine, point `ApiBaseUrl` at the backend's LAN IP,
e.g. `http://192.168.1.50:8000/api`.

## 9. Testing

```bash
cd backend
.venv\Scripts\python -m pytest
```

Backend tests cover health, document CRUD, published/unpublished filtering,
invalid ids/input, events, related content, metadata + OCR-text search,
media/people/topics, the admin key gate, and the Phase E OCR pipeline (upload,
validation failures, multi-page PDF OCR, per-page retrieval, original
preservation, missing documents/pages, OCR failure, status transitions). Tests
run against an in-memory SQLite engine (same ORM models as PostgreSQL) with a
fake OCR engine, so they are deterministic and need no tesseract. Live
end-to-end (Unity ↔ API ↔ DB) is exercised from the Unity regression suite with
`AMBEKAR_API_URL=http://127.0.0.1:8000/api`.

## 10. OCR pipeline (Phase E)

Upload flow: `POST /api/documents/upload` validates the file, saves the
original byte-for-byte under `STORAGE_ROOT/originals/{document_id}.{ext}`, and
kicks off OCR. Per-page results land in `ocr_results` (stable id
`{document_id}-p{n}`, `page_number`, `extracted_text`, `confidence`,
`processing_status`), and the aggregate transcript is written to
`documents.ocr_text` so the archive is full-text searchable and Phase F can
consume it directly.

- **Engine abstraction** (`app/ocr/`): `OcrEngine` ABC with pluggable
  implementations. `rapid` (default) is pure-pip/offline and verified on this
  machine; `tesseract` is code-ready for `en/hi/ta` when the binary + tessdata
  exist. Selection via `OCR_ENGINE` (`rapid` / `tesseract` / `auto`).
- **Text cleaning** is conservative: line-ending + NFC normalization, removal
  of detectable repeated headers/footers (≥3 occurrences), whitespace collapse
  — no rewriting of historical text.
- **Document-level status**: `NONE → PENDING → PROCESSING → COMPLETED/FAILED`
  with `ocr_error` explaining failures (corrupt PDF, engine unavailable, empty
  result, missing original …).
- **Original downloads** (`/documents/{id}/original`) serve the preserved scan
  read-only; OCR output never replaces it.

## 11. Schema summary

Documents, document_versions, document_metadata, media, events, people,
speeches, books, topics, relationships, ocr_results.

- Stable string primary keys everywhere (`AMB-SAM-001`, …).
- `relationships(source_id, target_id, relationship_type)` is entity-agnostic
  and open-typed (`related`, `event`, `person`, `topic`, `speaker`, `author_of`).
- `document_metadata(key, value)` keeps the model extensible without
  migrations for every new field.
- `media.media_type` ∈ {IMAGE, AUDIO, VIDEO, PDF, DOCUMENT_SCAN}.
- `ocr_results` holds one row per OCR page (FK → `documents`, CASCADE); the
  document row carries `ocr_status` / `ocr_language` / `ocr_error` for cheap
  status reads. `documents.ocr_text` is the aggregated transcript.

## 12. Roadmap (later phases)

Phase F embeddings/semantic search (pgvector), G RAG, H context AI, I voice,
J translation/multilingual models, K timeline, L knowledge mapping,
M hand tracking, N performance, O production/Quest build.