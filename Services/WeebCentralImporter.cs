using AnyComic.Models;
using HtmlAgilityPack;
using System.Net;
using System.Text.RegularExpressions;

namespace AnyComic.Services
{
    /// <summary>
    /// Imports manga metadata and chapter page URLs from weebcentral.com.
    /// Images are NOT downloaded — external URLs are indexed and stored directly.
    /// </summary>
    public class WeebCentralImporter
    {
        private readonly HttpClient _httpClient;
        private const string BASE_URL = "https://weebcentral.com";

        public WeebCentralImporter(string? proxyUrl = null)
        {
            var handler = new HttpClientHandler();

            if (!string.IsNullOrEmpty(proxyUrl))
            {
                handler.Proxy = new WebProxy(proxyUrl);
                handler.UseProxy = true;
                Console.WriteLine($"Using proxy: {proxyUrl}");
            }

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(60);

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        }

        #region DTOs

        public class WeebCentralChapter
        {
            public string  Id             { get; set; } = string.Empty;
            public decimal ChapterNumber  { get; set; }
            public string  ChapterTitle   { get; set; } = string.Empty;
        }

        public class ChapterImportData
        {
            public string       ChapterNumber { get; set; } = string.Empty;
            public string?      ChapterTitle  { get; set; }
            /// <summary>External image URLs from the source site (no local copies).</summary>
            public List<string> PageUrls      { get; set; } = new();
        }

        #endregion

        #region Public Methods

        public async Task<(Manga manga, List<ChapterImportData> chapters)?> ImportFromUrl(
            string url,
            string chapterRange = "all")
        {
            try
            {
                var seriesId = ExtractSeriesId(url);
                if (seriesId == null)
                {
                    Console.WriteLine("Invalid WeebCentral URL");
                    return null;
                }

                var mangaHtml = await _httpClient.GetStringAsync(url);
                var mangaDoc = new HtmlDocument();
                mangaDoc.LoadHtml(mangaHtml);

                var title       = ExtractTitle(mangaDoc)       ?? "Unknown Manga";
                var author      = ExtractAuthor(mangaDoc)      ?? "Unknown";
                var description = ExtractDescription(mangaDoc) ?? "Imported from WeebCentral";
                var coverUrl    = ExtractCoverUrl(mangaDoc);

                Console.WriteLine($"Found manga: {title} by {author}");

                var allChapters = await GetFullChapterList(seriesId);
                if (allChapters.Count == 0)
                {
                    Console.WriteLine("No chapters found");
                    return null;
                }

                Console.WriteLine($"Found {allChapters.Count} chapters");

                var selectedChapters = FilterChaptersByRange(allChapters, chapterRange);
                Console.WriteLine($"Indexing {selectedChapters.Count} chapters...");

                var manga = new Manga
                {
                    Titulo      = CleanTitle(title),
                    Autor       = author,
                    Descricao   = CleanDescription(description),
                    DataCriacao = DateTime.Now,
                    ImagemCapa  = coverUrl ?? "/images/placeholder.jpg"
                };

                var importedChapters = new List<ChapterImportData>();
                int index = 1;

                foreach (var chapter in selectedChapters.OrderBy(c => c.ChapterNumber))
                {
                    try
                    {
                        Console.WriteLine($"Indexing chapter {index}/{selectedChapters.Count}: {chapter.ChapterNumber}");

                        var pageUrls = await IndexChapterPages(chapter);

                        if (pageUrls.Count > 0)
                        {
                            importedChapters.Add(new ChapterImportData
                            {
                                ChapterNumber = chapter.ChapterNumber.ToString(),
                                ChapterTitle  = string.IsNullOrEmpty(chapter.ChapterTitle) ? null : chapter.ChapterTitle,
                                PageUrls      = pageUrls
                            });
                            Console.WriteLine($"  Indexed {pageUrls.Count} pages");
                        }

                        index++;
                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error indexing chapter {chapter.ChapterNumber}: {ex.Message}");
                    }
                }

                if (importedChapters.Count == 0)
                {
                    Console.WriteLine("No chapters were successfully indexed");
                    return null;
                }

                return (manga, importedChapters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing from WeebCentral: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Private Methods - Indexing

        private async Task<List<string>> IndexChapterPages(WeebCentralChapter chapter)
        {
            var pageUrls = new List<string>();

            try
            {
                var imagesUrl = $"{BASE_URL}/chapters/{chapter.Id}/images?is_prev=False&current_page=1&reading_style=long_strip";
                var html = await _httpClient.GetStringAsync(imagesUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var imgNodes = doc.DocumentNode.SelectNodes("//img[@alt]");
                if (imgNodes == null) return pageUrls;

                foreach (var img in imgNodes)
                {
                    var alt = img.GetAttributeValue("alt", "");
                    if (!alt.StartsWith("Page ", StringComparison.OrdinalIgnoreCase)) continue;

                    var src = img.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(src) && (src.Contains(".png") || src.Contains(".jpg") || src.Contains(".webp")))
                        pageUrls.Add(src);
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

        #region Private Methods - Metadata Extraction

        private string? ExtractSeriesId(string url)
        {
            var match = Regex.Match(url, @"weebcentral\.com/series/([A-Z0-9]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private string? ExtractTitle(HtmlDocument doc)
        {
            var ogTitle = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
            if (ogTitle != null)
            {
                var content = ogTitle.GetAttributeValue("content", "");
                var pipeIndex = content.IndexOf(" | ");
                return pipeIndex > 0 ? content[..pipeIndex].Trim() : content.Trim();
            }
            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            if (titleNode != null)
            {
                var text = titleNode.InnerText;
                var pipeIndex = text.IndexOf(" | ");
                return pipeIndex > 0 ? text[..pipeIndex].Trim() : text.Trim();
            }
            return null;
        }

        private string? ExtractAuthor(HtmlDocument doc)
        {
            var detailNodes = doc.DocumentNode.SelectNodes("//li[contains(@class, 'flex')]//span");
            if (detailNodes != null)
            {
                foreach (var node in detailNodes)
                {
                    var text = node.InnerText.Trim();
                    if (text == "Author(s)" || text == "Author")
                    {
                        var linkNode = node.ParentNode?.SelectSingleNode(".//a");
                        if (linkNode != null)
                            return HtmlEntity.DeEntitize(linkNode.InnerText.Trim());
                    }
                }
            }
            return null;
        }

        private string? ExtractDescription(HtmlDocument doc)
        {
            var ogDesc = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']");
            if (ogDesc != null)
            {
                var content = ogDesc.GetAttributeValue("content", "");
                if (!string.IsNullOrEmpty(content)) return HtmlEntity.DeEntitize(content);
            }
            return null;
        }

        private string? ExtractCoverUrl(HtmlDocument doc)
        {
            var ogImage = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            if (ogImage != null)
            {
                var content = ogImage.GetAttributeValue("content", "");
                return string.IsNullOrEmpty(content) ? null : content;
            }
            return null;
        }

        #endregion

        #region Private Methods - Chapter List

        private async Task<List<WeebCentralChapter>> GetFullChapterList(string seriesId)
        {
            var chapters = new List<WeebCentralChapter>();

            try
            {
                var url  = $"{BASE_URL}/series/{seriesId}/full-chapter-list";
                var html = await _httpClient.GetStringAsync(url);
                var doc  = new HtmlDocument();
                doc.LoadHtml(html);

                var chapterLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/chapters/')]");
                if (chapterLinks == null) return chapters;

                foreach (var link in chapterLinks)
                {
                    var href = link.GetAttributeValue("href", "");
                    if (string.IsNullOrEmpty(href) || !href.Contains("/chapters/")) continue;

                    var chapterText = link.InnerText.Trim();
                    var match = Regex.Match(chapterText, @"Chapter\s+([\d.]+)", RegexOptions.IgnoreCase);
                    if (!match.Success) continue;

                    if (!decimal.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal chapterNum)) continue;

                    var idMatch = Regex.Match(href, @"/chapters/([A-Z0-9]+)", RegexOptions.IgnoreCase);
                    if (!idMatch.Success) continue;

                    var titleMatch = Regex.Match(chapterText, @"Chapter\s+[\d.]+\s*[:\-]\s*(.+)", RegexOptions.IgnoreCase);

                    chapters.Add(new WeebCentralChapter
                    {
                        Id            = idMatch.Groups[1].Value,
                        ChapterNumber = chapterNum,
                        ChapterTitle  = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : ""
                    });
                }

                chapters = chapters
                    .GroupBy(c => c.ChapterNumber)
                    .Select(g => g.First())
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching chapter list: {ex.Message}");
            }

            return chapters;
        }

        #endregion

        #region Private Methods - Chapter Filtering

        private List<WeebCentralChapter> FilterChaptersByRange(List<WeebCentralChapter> allChapters, string range)
        {
            if (range.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
                return allChapters;

            var selectedNumbers = ExpandChapterRange(range);
            if (selectedNumbers == null || selectedNumbers.Count == 0)
                return allChapters;

            return allChapters
                .Where(c => selectedNumbers.Any(n => Math.Abs(n - c.ChapterNumber) < 0.01m))
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

        #endregion

        #region Private Methods - String Utilities

        private static string CleanTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "Unknown Manga";
            return (title.Length > 200 ? title[..200] : title).Trim();
        }

        private static string CleanDescription(string? description)
        {
            if (string.IsNullOrEmpty(description)) return "Imported from WeebCentral";
            return (description.Length > 1000 ? description[..1000] + "..." : description).Trim();
        }

        #endregion
    }
}
