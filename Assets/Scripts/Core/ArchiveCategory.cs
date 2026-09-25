namespace AmbedkarHeritage.Core
{
    /// <summary>
    /// Archive navigation buckets. The UI filter exposes All..Videos; Event and
    /// Unknown records are internally mapped to Uncategorized (visible only
    /// under "ALL") until the timeline phase gives them a real bucket.
    /// </summary>
    public enum ArchiveCategory
    {
        All = 0,
        Manuscripts = 1,
        Books = 2,
        Documents = 3,
        Photographs = 4,
        Speeches = 5,
        Videos = 6,
        Uncategorized = 99
    }

    public static class CategoryMap
    {
        /// <summary>Filter order shown in the VR category bar (requirement: ALL, MANUSCRIPTS, BOOKS, DOCUMENTS, PHOTOGRAPHS, SPEECHES, VIDEOS).</summary>
        public static readonly ArchiveCategory[] FilterOrder =
        {
            ArchiveCategory.All,
            ArchiveCategory.Manuscripts,
            ArchiveCategory.Books,
            ArchiveCategory.Documents,
            ArchiveCategory.Photographs,
            ArchiveCategory.Speeches,
            ArchiveCategory.Videos
        };

        public static string Label(ArchiveCategory category)
        {
            switch (category)
            {
                case ArchiveCategory.All: return "ALL";
                case ArchiveCategory.Manuscripts: return "MANUSCRIPTS";
                case ArchiveCategory.Books: return "BOOKS";
                case ArchiveCategory.Documents: return "DOCUMENTS";
                case ArchiveCategory.Photographs: return "PHOTOGRAPHS";
                case ArchiveCategory.Speeches: return "SPEECHES";
                case ArchiveCategory.Videos: return "VIDEOS";
                default: return "ALL";
            }
        }

        /// <summary>Maps a record type to its filter bucket. Letter record scan into Documents; Event stays uncategorized for now.</summary>
        public static ArchiveCategory For(ArchiveRecordType type)
        {
            switch (type)
            {
                case ArchiveRecordType.Manuscript: return ArchiveCategory.Manuscripts;
                case ArchiveRecordType.Book: return ArchiveCategory.Books;
                case ArchiveRecordType.Document:
                case ArchiveRecordType.Letter: return ArchiveCategory.Documents;
                case ArchiveRecordType.Photograph: return ArchiveCategory.Photographs;
                case ArchiveRecordType.Speech:
                case ArchiveRecordType.Audio: return ArchiveCategory.Speeches;
                case ArchiveRecordType.Video: return ArchiveCategory.Videos;
                default: return ArchiveCategory.Uncategorized;
            }
        }
    }
}