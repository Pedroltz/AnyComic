namespace AnyComic.Domain.Interfaces;

/// <summary>A chapter as found on the source site, before its pages are indexed.</summary>
public class WeebCentralChapter
{
    public string  Id            { get; set; } = string.Empty;
    public decimal ChapterNumber { get; set; }
    public string  ChapterTitle  { get; set; } = string.Empty;
}

/// <summary>A chapter ready to be persisted (metadata + optionally its page URLs).</summary>
public class ChapterImportData
{
    public string       ChapterNumber   { get; set; } = string.Empty;
    public string?      ChapterTitle    { get; set; }
    /// <summary>External image URLs from the source site (no local copies).</summary>
    public List<string> PageUrls        { get; set; } = new();
    /// <summary>Chapter ID on the source site, used for lazy page indexing later.</summary>
    public string?      FonteCapituloId { get; set; }
}

/// <summary>A series found while enumerating the WeebCentral catalog.</summary>
public record CatalogEntry(string SeriesId, string Url, string Title, string? CoverUrl);
