# Archive Schema — Ambedkar Digital Heritage

Canonical field reference for `ArchiveRecord` (Unity `Assets/Scripts/Core/ArchiveRecord.cs`)
and the demo dataset (`Assets/Resources/Data/sample_archive.json`). The backend API
(`backend/`, later phases) must serialize records with the same field names so the
Unity client can swap demo data for live data without code changes.

## Design rules

- **Data-driven**: every archival fact lives in data, never in Unity scripts.
- **Backward compatible**: all original fields are kept. Every field except `id`
  is optional at the serializer level; new fields default to null/empty/false.
- **JsonUtility conventions**: field names map 1:1 to JSON keys (case-sensitive).
  Enums are encoded as their integer value (see `ArchiveRecordType`).
  Use `ArchiveRecord.ParseType(string)` only for non-JsonUtility/backend input.
- **Never nullable collections**: `relatedIds` and `tags` are always non-null
  lists. `ArchiveRecord.EnsureSafeDefaults()` normalizes them after any
  deserialization; `DemoArchiveLoader` calls it for every loaded record.
- **Multimedia are references, not content**: `image`, `audio`, `video`,
  `document` hold URIs/asset paths. Content is never embedded in the record.

## Field reference

| Field            | Type         | Required | JSON key    | Notes |
|------------------|--------------|----------|-------------|-------|
| `id`             | string       | yes      | `id`        | Stable unique identifier (e.g. `AMB-SAM-006`). |
| `type`           | enum (int)   | no*      | `type`      | `ArchiveRecordType` integer. *Practically required; `Unknown` (0) marks unresolved data. |
| `title`          | string       | no       | `title`     | Display title. |
| `description`    | string       | no       | `description` | Archival summary. |
| `date`           | string       | no       | `date`      | Display date (e.g. `1916`). Kept as string; structured date normalization lands with the backend. |
| `category`       | string       | no       | `category`  | Collection category (Manuscripts, Speeches, ...). |
| `source`         | string       | no       | `source`    | Provenance (institution, scan batch, `sample-dataset`, ...). |
| `language`       | string       | no       | `language`  | Content language code (`en`, `hi`, `ta`). |
| `isSampleData`   | bool         | no       | `isSampleData` | True for demo/placeholder records (suppresses "verified" styling). |
| `verified`       | bool         | no       | `verified`  | True only after institutional verification. |
| `citation`       | string       | no       | `citation`  | Human-readable citation/reference. |
| `relatedIds`     | string[]     | no       | `relatedIds` | Stable ids of related records. Also called "related record IDs" in the brief. |
| `eventId`        | string       | no       | `eventId`   | Stable id of the timeline event this record belongs to. |
| `image`          | string       | no       | `image`     | Reference to representative image. |
| `audio`          | string       | no       | `audio`     | Reference to audio asset (speech/narration). |
| `video`          | string       | no       | `video`     | Reference to video asset. |
| `document`       | string       | no       | `document`  | Reference to original scanned document. |
| `ocrText`        | string       | no       | `ocrText`   | OCR-extracted text (Phase F pipeline output; search/RAG operate on this). |
| `pages`          | string[]     | no       | `pages`     | Ordered image-page references for the document viewer (multi-page). Falls back to `image`/`document` when absent. |
| `tags`           | string[]     | no       | `tags`      | Free-form search/knowledge-mapping tags. |

Absent optional fields deserialize to: `null` (strings), `false` (bools),
`0/Unknown` (enum), empty list (collections via constructor + `EnsureSafeDefaults`).

## Demo dataset

`sample_archive.json` contains `config` (MuseumConfig: `appName`,
`welcomeMessage`, `supportedLanguages`) and `records` (7 records, all flagged
`isSampleData: true` so the UI never presents them as verified):

| id            | type      | extended fields exercised                              |
|---------------|-----------|--------------------------------------------------------|
| `AMB-SAM-001` | Manuscript (1) | image, document, ocrText, tags, citation          |
| `AMB-SAM-002` | Document (5)   | document, ocrText, tags, citation                 |
| `AMB-SAM-003` | Photograph (3) | image, tags, citation (no audio/video — omitted)  |
| `AMB-SAM-004` | Speech (6)     | audio, document (transcript), ocrText, tags       |
| `AMB-SAM-005` | Event (9)      | eventId `TL-1891`, tags, citation                 |
| `AMB-SAM-006` | Book (2)       | image, document, pages (3), ocrText, tags, citation (new record) |
| `AMB-SAM-007` | Letter (4)     | document, pages (1), ocrText, tags, citation (new record) |
| `AMB-SAM-008` | Video (8)      | video, tags, citation (new record)                      |

## Archive Room layout (`Resources/Data/archive_room.json`)

Data-driven placement of exhibit stations (Phase C). One JSON per record — nothing
exhibit-specific is hard-coded:

```json
{ "room": {
    "roomName": "Archive Room (Demo)",
    "accentColor": [0.55, 0.35, 0.12],
    "boardColor": [0.2, 0.42, 0.55],
    "stations": [
      { "exhibitId": "AMB-SAM-001", "displayType": "manuscript",
        "position": [-6.3, 0, 2.6], "rotation": [0, 180, 0], "scale": 1 }
    ]
} }
```

- `displayType` overrides station appearance: `manuscript|book|document|letter|photograph|event` (board),
  `speech|audio` (speaker), `video` (screen); `""` = derive from record type.
- Loader: `ArchiveRoomDataLoader` (built-in fallback layout if the file is missing/invalid).
- Builder: `MuseumRoomBuilder.BuildMuseumRoom` creates stations, the archive info panel,
  the document viewer and the category filter from this data. Used by `MainMuseumBuilder`
  (scene baking) and the regression checks (temp scene).

## Consumers

- `DemoArchiveLoader` — loads/normalizes demo records (`EnsureSafeDefaults`).
- `ExhibitController` — builds exhibits from records (title/description/source).
- `VrSession` — tracks the current exhibit id (future AI/RAG context).
- `FoundationRegressionChecks` — validates schema, extended fields, and
  missing-optional-field safety in CI/batch mode.

## Later phases (not implemented yet)

- Backend serialization (FastAPI/PostgreSQL) must emit these exact field names,
  plus pagination metadata, via the API layer.
- OCR pipeline (Phase F) populates `ocrText`; semantic/RAG (Phases G/I) indexes
  `ocrText` + `description` + `tags`.
- Verification workflow flips `verified`; UI gates "verified" styling on it.