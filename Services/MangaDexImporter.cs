using AnyComic.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AnyComic.Services
{
    /// <summary>
    /// Imports manga metadata and chapter page URLs from MangaDex.org.
    /// Uses MangaDex API v5: https://api.mangadex.org/docs/
    /// Images are NOT downloaded — external URLs (uploads.mangadex.org) are stored directly.
    /// </summary>
    public class MangaDexImporter
    {
        private readonly HttpClient _httpClient;
        private const string BASE_URL         = "https://api.mangadex.org";
        private const string UPLOADS_BASE_URL = "https://uploads.mangadex.org";

        public MangaDexImporter()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AnyComic/1.0");
        }

        #region DTOs

        public class MangaDexMangaInfo
        {
            public string   Id              { get; set; } = string.Empty;
            public string   Title           { get; set; } = string.Empty;
            public string?  Author          { get; set; }
            public string?  Artist          { get; set; }
            public string?  Description     { get; set; }
            public string?  CoverId         { get; set; }
            public string?  CoverFileName   { get; set; }
            public DateTime? CreatedAt      { get; set; }
        }

        public class MangaDexChapter
        {
            public string   Id       { get; set; } = string.Empty;
            public string?  Chapter  { get; set; }
            public string?  Title    { get; set; }
            public string?  Volume   { get; set; }
            public string   Language { get; set; } = string.Empty;
            public int      Pages    { get; set; }
            public DateTime? PublishAt { get; set; }
        }

        public class ChapterImportData
        {
            public string       ChapterNumber { get; set; } = string.Empty;
            public string?      ChapterTitle  { get; set; }
            /// <summary>External image URLs from uploads.mangadex.org (no local copies).</summary>
            public List<string> PageUrls      { get; set; } = new();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Indexes a manga from MangaDex. Fetches metadata and chapter page URLs via the API;
        /// no images are downloaded or stored locally.
        /// </summary>
        public async Task<(Manga manga, List<ChapterImportData> chapters)?> ImportFromUrl(
            string url,
            string language     = "en",
            string chapterRange = "all",
            string quality      = "data")
        {
            try
            {
                var mangaId = ExtractMangaIdFromUrl(url);
                if (mangaId == null)
                {
                    Console.WriteLine("Invalid MangaDex URL");
                    return null;
                }

                var mangaInfo = await GetMangaInfo(mangaId);
                if (mangaInfo == null)
                {
                    Console.WriteLine("Failed to fetch manga information");
                    return null;
                }

                var allChapters = await GetChapters(mangaId, language);
                if (allChapters.Count == 0)
                {
                    Console.WriteLine($"No chapters found in language: {language}");
                    return null;
                }

                Console.WriteLine($"Found {allChapters.Count} chapters in {language}");

                var selectedChapters = FilterChaptersByRange(allChapters, chapterRange);
                if (selectedChapters.Count == 0)
                {
                    Console.WriteLine($"No chapters match the range: {chapterRange}");
                    return null;
                }

                Console.WriteLine($"Indexing {selectedChapters.Count} chapters...");

                // Build cover URL through the proxy so hotlink protection is bypassed
                string coverUrl = "/images/placeholder.jpg";
                if (!string.IsNullOrEmpty(mangaInfo.CoverFileName))
                    coverUrl = Proxied($"{UPLOADS_BASE_URL}/covers/{mangaId}/{mangaInfo.CoverFileName}");

                var manga = new Manga
                {
                    Titulo      = CleanTitle(mangaInfo.Title),
                    Autor       = mangaInfo.Author ?? mangaInfo.Artist ?? "Unknown",
                    Descricao   = CleanDescription(mangaInfo.Description),
                    DataCriacao = mangaInfo.CreatedAt ?? DateTime.Now,
                    ImagemCapa  = coverUrl
                };

                var indexedChapters = new List<ChapterImportData>();
                int chapterIndex = 1;

                foreach (var chapter in selectedChapters.OrderBy(c => ParseChapterNumber(c.Chapter)))
                {
                    try
                    {
                        Console.WriteLine($"Indexing chapter {chapterIndex}/{selectedChapters.Count}: {chapter.Chapter}");

                        var pageUrls = await IndexChapterPages(chapter.Id, quality);

                        if (pageUrls.Count > 0)
                        {
                            indexedChapters.Add(new ChapterImportData
                            {
                                ChapterNumber = chapter.Chapter ?? "0",
                                ChapterTitle  = chapter.Title,
                                PageUrls      = pageUrls
                            });
                            Console.WriteLine($"  Indexed {pageUrls.Count} pages");
                        }
                        else
                        {
                            Console.WriteLine($"  No pages found for chapter {chapter.Chapter}");
                        }

                        chapterIndex++;
                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error indexing chapter {chapter.Chapter}: {ex.Message}");
                    }
                }

                if (indexedChapters.Count == 0)
                {
                    Console.WriteLine("No chapters were successfully indexed");
                    return null;
                }

                return (manga, indexedChapters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing from MangaDex: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Private Methods - Indexing

        /// <summary>
        /// Calls the MangaDex at-home API to get page image URLs for a chapter.
        /// Uses the canonical uploads.mangadex.org base so URLs are permanent.
        /// </summary>
        private async Task<List<string>> IndexChapterPages(string chapterId, string quality)
        {
            var pageUrls = new List<string>();

            try
            {
                var atHomeUrl = $"{BASE_URL}/at-home/server/{chapterId}";
                var response  = await _httpClient.GetStringAsync(atHomeUrl);
                var json      = JsonDocument.Parse(response);

                var chapter = json.RootElement.GetProperty("chapter");
                var hash    = chapter.GetProperty("hash").GetString();

                // Prefer canonical CDN over the dynamic baseUrl mirror
                var qualityPath  = quality == "data-saver" ? "data-saver" : "data";
                var imageArray   = quality == "data-saver"
                    ? chapter.GetProperty("dataSaver")
                    : chapter.GetProperty("data");

                foreach (var img in imageArray.EnumerateArray())
                {
                    var imageName = img.GetString();
                    if (!string.IsNullOrEmpty(imageName))
                        pageUrls.Add(Proxied($"{UPLOADS_BASE_URL}/{qualityPath}/{hash}/{imageName}"));
                }

                Console.WriteLine($"  Found {pageUrls.Count} pages");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error indexing chapter pages: {ex.Message}");
            }

            return pageUrls;
        }

        #endregion

        #region Private Methods - URL & Validation

        private string? ExtractMangaIdFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            var match = Regex.Match(url, @"mangadex\.org/title/([a-f0-9-]{36})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        #endregion

        #region Private Methods - API Calls

        private async Task<MangaDexMangaInfo?> GetMangaInfo(string mangaId)
        {
            try
            {
                var url      = $"{BASE_URL}/manga/{mangaId}?includes[]=cover_art&includes[]=author&includes[]=artist";
                var response = await _httpClient.GetStringAsync(url);
                var json     = JsonDocument.Parse(response);

                var data       = json.RootElement.GetProperty("data");
                var attributes = data.GetProperty("attributes");

                var titleObj = attributes.GetProperty("title");
                string title = GetLocalizedString(titleObj, "en");

                string? description = null;
                if (attributes.TryGetProperty("description", out var descObj))
                    description = GetLocalizedString(descObj, "en");

                string? author = null, artist = null, coverId = null, coverFileName = null;

                if (data.TryGetProperty("relationships", out var relationships))
                {
                    foreach (var rel in relationships.EnumerateArray())
                    {
                        var type = rel.GetProperty("type").GetString();

                        if (type == "author" && rel.TryGetProperty("attributes", out var authorAttrs))
                            author = authorAttrs.GetProperty("name").GetString();
                        else if (type == "artist" && rel.TryGetProperty("attributes", out var artistAttrs))
                            artist = artistAttrs.GetProperty("name").GetString();
                        else if (type == "cover_art")
                        {
                            coverId = rel.GetProperty("id").GetString();
                            if (rel.TryGetProperty("attributes", out var coverAttrs))
                                coverFileName = coverAttrs.GetProperty("fileName").GetString();
                        }
                    }
                }

                return new MangaDexMangaInfo
                {
                    Id             = mangaId,
                    Title          = title,
                    Author         = author,
                    Artist         = artist,
                    Description    = description,
                    CoverId        = coverId,
                    CoverFileName  = coverFileName
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching manga info: {ex.Message}");
                return null;
            }
        }

        private async Task<List<MangaDexChapter>> GetChapters(string mangaId, string language)
        {
            var chapters = new List<MangaDexChapter>();
            int offset = 0;
            const int limit = 500;

            try
            {
                while (true)
                {
                    var url      = $"{BASE_URL}/manga/{mangaId}/feed?translatedLanguage[]={language}&limit={limit}&offset={offset}&order[chapter]=asc&contentRating[]=safe&contentRating[]=suggestive&contentRating[]=erotica&contentRating[]=pornographic";
                    var response = await _httpClient.GetStringAsync(url);
                    var json     = JsonDocument.Parse(response);

                    var data  = json.RootElement.GetProperty("data");
                    var total = json.RootElement.GetProperty("total").GetInt32();

                    foreach (var chapterData in data.EnumerateArray())
                    {
                        var attributes = chapterData.GetProperty("attributes");

                        // Skip chapters that only exist as external links (e.g., MangaPlus)
                        var externalUrl = attributes.TryGetProperty("externalUrl", out var extUrl) ? extUrl.GetString() : null;
                        var pageCount   = attributes.GetProperty("pages").GetInt32();
                        if (!string.IsNullOrEmpty(externalUrl) || pageCount == 0) continue;

                        chapters.Add(new MangaDexChapter
                        {
                            Id       = chapterData.GetProperty("id").GetString() ?? "",
                            Chapter  = attributes.TryGetProperty("chapter", out var ch) ? ch.GetString() : null,
                            Title    = attributes.TryGetProperty("title",   out var t)  ? t.GetString()  : null,
                            Volume   = attributes.TryGetProperty("volume",  out var v)  ? v.GetString()  : null,
                            Language = attributes.GetProperty("translatedLanguage").GetString() ?? "",
                            Pages    = pageCount
                        });
                    }

                    if (offset + limit >= total) break;
                    offset += limit;
                    await Task.Delay(500);
                }

                // Deduplicate: keep the version with most pages for each chapter number
                var deduplicated = chapters
                    .GroupBy(c => c.Chapter ?? "0")
                    .Select(g => g.OrderByDescending(c => c.Pages).First())
                    .ToList();

                Console.WriteLine($"Found {chapters.Count} chapters, {deduplicated.Count} after deduplication");
                return deduplicated;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching chapters: {ex.Message}");
                return chapters;
            }
        }

        #endregion

        #region Private Methods - Chapter Filtering

        private List<MangaDexChapter> FilterChaptersByRange(List<MangaDexChapter> allChapters, string range)
        {
            if (range.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
                return allChapters;

            var selectedNumbers = ExpandChapterRange(range);
            if (selectedNumbers == null || selectedNumbers.Count == 0)
                return allChapters;

            return allChapters
                .Where(c => selectedNumbers.Any(n => Math.Abs(n - ParseChapterNumber(c.Chapter)) < 0.01m))
                .ToList();
        }

        private static List<decimal>? ExpandChapterRange(string range)
        {
            if (string.IsNullOrWhiteSpace(range) || range.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
                return null;

            var chapters = new HashSet<decimal>();

            foreach (var part in range.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Contains('-'))
                {
                    var sides = trimmed.Split('-');
                    if (sides.Length == 2
                        && decimal.TryParse(sides[0].Trim(), out decimal start)
                        && decimal.TryParse(sides[1].Trim(), out decimal end))
                    {
                        for (decimal i = Math.Ceiling(start); i <= Math.Floor(end); i++)
                            chapters.Add(i);
                    }
                }
                else if (decimal.TryParse(trimmed, out decimal single))
                {
                    chapters.Add(single);
                }
            }

            return chapters.OrderBy(c => c).ToList();
        }

        private static decimal ParseChapterNumber(string? chapterStr)
            => decimal.TryParse(chapterStr, out var result) ? result : 0;

        #endregion

        #region Private Methods - String Utilities

        private string GetLocalizedString(JsonElement obj, string preferredLang)
        {
            if (obj.TryGetProperty(preferredLang, out var preferred))
                return preferred.GetString() ?? "Unknown";

            foreach (var alt in new[] { "en", "en-us", "ja-ro", "ja" })
                if (obj.TryGetProperty(alt, out var altValue))
                    return altValue.GetString() ?? "Unknown";

            foreach (var prop in obj.EnumerateObject())
            {
                var value = prop.Value.GetString();
                if (!string.IsNullOrEmpty(value)) return value;
            }

            return "Unknown";
        }

        /// <summary>
        /// Wraps an external URL through the app's image proxy so the correct
        /// Referer header is sent automatically when the browser loads the image.
        /// </summary>
        private static string Proxied(string externalUrl)
            => $"/Proxy/Image?url={Uri.EscapeDataString(externalUrl)}";

        private static string CleanTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "Unknown Manga";
            return (title.Length > 200 ? title[..200] : title).Trim();
        }

        private static string CleanDescription(string? description)
        {
            if (string.IsNullOrEmpty(description)) return "Imported from MangaDex.org";
            return (description.Length > 1000 ? description[..1000] + "..." : description).Trim();
        }

        #endregion
    }
}
