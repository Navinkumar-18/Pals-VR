using System;
using System.Collections.Generic;

namespace AmbedkarHeritage.Core
{
    public enum ArchiveRecordType
    {
        // NOTE: JsonUtility serializes enums as their INTEGER value, so JSON
        // data must use these numbers (e.g. "type": 1 for Manuscript).
        // ArchiveRecord.ParseType() additionally accepts human-readable strings
        // for data arriving from non-JsonUtility sources (backend API, tools).
        Unknown = 0,
        Manuscript = 1,
        Book = 2,
        Photograph = 3,
        Letter = 4,
        Document = 5,
        Speech = 6,
        Audio = 7,
        Video = 8,
        Event = 9
    }

    /// <summary>
    /// Stable, schema-driven archival record (Phase B: full digital-heritage
    /// archive model). No archival content is hard-coded anywhere; the content
    /// lives in data (sample JSON today, backend API later).
    ///
    /// Backward compatibility: every field is OPTIONAL unless marked required.
    /// Records written for the original model (id/type/title/description/date/
    /// category/source/language/isSampleData/relatedIds) load unchanged; all
    /// new fields default to null/empty/false and never crash code that reads
    /// them. Call <see cref="EnsureSafeDefaults"/> after deserialization to
    /// normalize collections that came through as null.
    /// </summary>
    [Serializable]
    public class ArchiveRecord
    {
        // ------------------------------------------------------------------
        // Core identity (required in practice; blank fields are tolerated)
        // ------------------------------------------------------------------
        public string id;

        /// <summary>Enum value (numeric in JSON). See ArchiveRecordType notes.</summary>
        public ArchiveRecordType type;

        public string title;
        public string description;
        public string date;
        public string category;

        // ------------------------------------------------------------------
        // Provenance / language / verification
        // ------------------------------------------------------------------
        /// <summary>Origin of the record (institution, scan batch, sample dataset, ...).</summary>
        public string source;

        /// <summary>Primary content language code, e.g. "en", "hi", "ta".</summary>
        public string language;

        /// <summary>True when this record is demo/placeholder data, not a verified archival item.</summary>
        public bool isSampleData;

        /// <summary>True only when the institution has verified the record (metadata + content).</summary>
        public bool verified;

        /// <summary>Human-readable citation/reference for the record (filled by the archive).</summary>
        public string citation;

        // ------------------------------------------------------------------
        // Relationships
        // ------------------------------------------------------------------
        /// <summary>Stable ids of related archival records (documents, speeches, photographs, events).</summary>
        public List<string> relatedIds;

        /// <summary>Stable id of the timeline/event this record belongs to, when applicable.</summary>
        public string eventId;

        // ------------------------------------------------------------------
        // Multimedia references (URIs / asset paths; content is never embedded)
        // ------------------------------------------------------------------
        /// <summary>Reference to a representative image (thumbnail/full view).</summary>
        public string image;

        /// <summary>Reference to an audio asset (narration, speech recording).</summary>
        public string audio;

        /// <summary>Reference to a video asset.</summary>
        public string video;

        /// <summary>Reference to the original scanned document (never replaced by derived text).</summary>
        public string document;

        // ------------------------------------------------------------------
        // Text & indexing
        // ------------------------------------------------------------------
        /// <summary>OCR-extracted text of the original document (Phase F pipeline output).</summary>
        public string ocrText;

        /// <summary>
        /// Ordered image-page references for the document viewer (multi-page).
        /// Optional: when absent the viewer falls back to image/document refs.
        /// </summary>
        public List<string> pages;

        /// <summary>Free-form tags used by search/knowledge mapping.</summary>
        public List<string> tags;

        public ArchiveRecord()
        {
            relatedIds = new List<string>();
            pages = new List<string>();
            tags = new List<string>();
        }

        /// <summary>
        /// Normalizes a record after deserialization so missing/optional fields
        /// can never cause null-reference failures. Safe to call multiple times.
        /// </summary>
        public void EnsureSafeDefaults()
        {
            if (relatedIds == null)
            {
                relatedIds = new List<string>();
            }

            if (pages == null)
            {
                pages = new List<string>();
            }

            if (tags == null)
            {
                tags = new List<string>();
            }
        }

        /// <summary>True when the record carries any OCR text or searchable metadata.</summary>
        public bool HasSearchableText
        {
            get
            {
                return !string.IsNullOrEmpty(ocrText)
                       || !string.IsNullOrEmpty(title)
                       || !string.IsNullOrEmpty(description)
                       || (tags != null && tags.Count > 0);
            }
        }

        public static ArchiveRecordType ParseType(string value)
        {
            if (Enum.TryParse(value, true, out ArchiveRecordType parsed))
            {
                return parsed;
            }

            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "manuscript":
                    return ArchiveRecordType.Manuscript;
                case "book":
                    return ArchiveRecordType.Book;
                case "photograph":
                    return ArchiveRecordType.Photograph;
                case "photo":
                    return ArchiveRecordType.Photograph;
                case "letter":
                    return ArchiveRecordType.Letter;
                case "document":
                case "scan":
                    return ArchiveRecordType.Document;
                case "speech":
                    return ArchiveRecordType.Speech;
                case "audio":
                    return ArchiveRecordType.Audio;
                case "video":
                    return ArchiveRecordType.Video;
                case "event":
                case "timeline":
                    return ArchiveRecordType.Event;
                default:
                    return ArchiveRecordType.Unknown;
            }
        }
    }

    /// <summary>
    /// Museum-level configuration loaded from data. The welcome/entrance message
    /// is data-driven and never baked into code.
    /// </summary>
    [Serializable]
    public class MuseumConfig
    {
        public string appName;
        public string welcomeMessage;
        public List<string> supportedLanguages;
    }

    [Serializable]
    public class DemoArchiveData
    {
        public MuseumConfig config;
        public List<ArchiveRecord> records;
    }
}