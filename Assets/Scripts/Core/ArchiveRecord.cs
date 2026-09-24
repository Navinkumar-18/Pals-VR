using System;
using System.Collections.Generic;

namespace AmbedkarHeritage.Core
{
    public enum ArchiveRecordType
    {
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
    /// Stable, schema-driven archival record. No archival content is hard-coded
    /// anywhere; the content lives in data (sample JSON today, backend API later).
    /// </summary>
    [Serializable]
    public class ArchiveRecord
    {
        public string id;
        public ArchiveRecordType type;
        public string title;
        public string description;
        public string date;
        public string category;
        public string source;
        public string language;
        public bool isSampleData;
        public List<string> relatedIds;

        public ArchiveRecord()
        {
            relatedIds = new List<string>();
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