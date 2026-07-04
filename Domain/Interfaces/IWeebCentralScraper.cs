using AnyComic.Models;

namespace AnyComic.Domain.Interfaces;

/// <summary>
/// Port for scraping manga metadata, chapter lists and page URLs from weebcentral.com.
/// The concrete implementation lives in the Infrastructure layer.
/// </summary>
public interface IWeebCentralScraper
{
    /// <summary>Full import: series metadata + chapter list + page URLs for the selected range.</summary>
    Task<(Manga manga, List<ChapterImportData> chapters)?> ImportFromUrl(string url, string chapterRange = "all");

    /// <summary>Shallow import: series metadata + chapter list only (pages indexed lazily later).</summary>
    Task<(Manga manga, List<ChapterImportData> chapters)?> ImportSeriesShallow(string url);

    /// <summary>Enumerates catalog series (popularity-sorted) up to <paramref name="maxSeries"/>.</summary>
    Task<List<CatalogEntry>> EnumerateCatalog(int maxSeries);

    /// <summary>Fetches the page image URLs for a single chapter.</summary>
    Task<List<string>> IndexChapterPages(WeebCentralChapter chapter, string mangaUrl);
}
