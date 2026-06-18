using HtmlAgilityPack;
using AnyComic.Models;
using System.Text.RegularExpressions;

namespace AnyComic.Services
{
    /// <summary>
    /// Imports manga metadata and chapter page URLs from mangalivre.blog.
    /// Images are NOT downloaded — external URLs are indexed and stored directly,
    /// so the reader loads them on demand from the source site.
    /// </summary>
    public class MangaLivreImporter
    {
        private readonly HttpClient _httpClient;

        public MangaLivreImporter()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        #region DTOs

        public class ChapterImportData
        {
            public string       ChapterNumber { get; set; } = string.Empty;
            public string?      ChapterTitle  { get; set; }
            /// <summary>External image URLs from the source site (no local copies).</summary>
            public List<string> PageUrls      { get; set; } = new();
        }

        private class ChapterInfo
        {
            public string  Url    { get; set; } = string.Empty;
            public string  Number { get; set; } = string.Empty;
            public string? Title  { get; set; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Indexes a manga from mangalivre.blog. Fetches metadata and chapter page URLs;
        /// no images are downloaded or stored locally.
        /// </summary>
        public async Task<(Manga manga, List<ChapterImportData> chapters)?> ImportFromUrl(
            string url,
            string chapterRange = "all")
        {
            try
            {
                if (!IsValidMangaLivreUrl(url))
                {
                    Console.WriteLine("Invalid MangaLivre URL");
                    return null;
                }

                var html = await _httpClient.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var manga = ExtractMangaMetadata(htmlDoc);
                if (manga == null)
                {
                    Console.WriteLine("Failed to extract manga metadata");
                    return null;
                }

                // Use the external cover URL directly (no local download)
                var coverUrl = NormalizeUrl(ExtractCoverUrl(htmlDoc));
                if (!string.IsNullOrEmpty(coverUrl))
                    manga.ImagemCapa = coverUrl;

                var allChapters = ExtractChapterList(htmlDoc);
                if (allChapters.Count == 0)
                {
                    Console.WriteLine("No chapters found");
                    return null;
                }

                Console.WriteLine($"Found {allChapters.Count} chapters");

                var selectedChapters = FilterChaptersByRange(allChapters, chapterRange);
                if (selectedChapters.Count == 0)
                {
                    Console.WriteLine($"No chapters match the range: {chapterRange}");
                    return null;
                }

                Console.WriteLine($"Indexing {selectedChapters.Count} chapters...");

                var indexedChapters = new List<ChapterImportData>();
                int i = 1;

                foreach (var chapter in selectedChapters.OrderBy(c => ParseChapterNumber(c.Number)))
                {
                    try
                    {
                        Console.WriteLine($"Indexing chapter {i}/{selectedChapters.Count}: {chapter.Number}");

                        var pageUrls = await IndexChapterPages(chapter.Url);

                        if (pageUrls.Count > 0)
                        {
                            indexedChapters.Add(new ChapterImportData
                            {
                                ChapterNumber = chapter.Number,
                                ChapterTitle  = chapter.Title,
                                PageUrls      = pageUrls
                            });
                            Console.WriteLine($"  Indexed {pageUrls.Count} pages");
                        }
                        else
                        {
                            Console.WriteLine($"  No pages found for chapter {chapter.Number}");
                        }

                        i++;
                        await Task.Delay(500); // polite delay between page fetches
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error indexing chapter {chapter.Number}: {ex.Message}");
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
                Console.WriteLine($"Error importing from MangaLivre: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Private Methods - Indexing

        /// <summary>
        /// Fetches a chapter page and returns the external URLs of all page images.
        /// No images are downloaded.
        /// </summary>
        private async Task<List<string>> IndexChapterPages(string chapterUrl)
        {
            var pageUrls = new List<string>();

            try
            {
                var html = await _httpClient.GetStringAsync(chapterUrl);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var imageNodes = htmlDoc.DocumentNode.SelectNodes("//img[contains(@class, 'chapter-image')]")
                             ?? htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'chapter-image-container')]//img")
                             ?? htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'chapter-images')]//img");

                if (imageNodes == null || imageNodes.Count == 0)
                {
                    Console.WriteLine("  No images found in chapter page");
                    return pageUrls;
                }

                Console.WriteLine($"  Found {imageNodes.Count} pages");

                foreach (var imgNode in imageNodes)
                {
                    // Handles lazy-loading: try src → data-src → data-lazy-src
                    var src = imgNode.GetAttributeValue("src", "");
                    if (string.IsNullOrEmpty(src) || src.StartsWith("data:"))
                        src = imgNode.GetAttributeValue("data-src", "");
                    if (string.IsNullOrEmpty(src) || src.StartsWith("data:"))
                        src = imgNode.GetAttributeValue("data-lazy-src", "");

                    var absoluteUrl = NormalizeUrl(src);
                    if (!string.IsNullOrEmpty(absoluteUrl))
                        pageUrls.Add(absoluteUrl);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error indexing chapter pages: {ex.Message}");
            }

            return pageUrls;
        }

        /// <summary>
        /// Ensures a URL is absolute. Relative paths are prefixed with the base domain.
        /// Returns null for empty or data: URIs.
        /// </summary>
        private static string? NormalizeUrl(string? url)
        {
            if (string.IsNullOrEmpty(url) || url.StartsWith("data:"))
                return null;

            if (url.StartsWith("http://") || url.StartsWith("https://"))
                return url;

            return "https://mangalivre.blog" + (url.StartsWith("/") ? "" : "/") + url;
        }

        #endregion

        #region Private Methods - Validation

        private static bool IsValidMangaLivreUrl(string url)
            => !string.IsNullOrEmpty(url) && url.Contains("mangalivre.blog/manga/");

        #endregion

        #region Private Methods - Metadata Extraction

        private Manga? ExtractMangaMetadata(HtmlDocument htmlDoc)
        {
            try
            {
                var titleNode = htmlDoc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'manga-title')]")
                             ?? htmlDoc.DocumentNode.SelectSingleNode("//h1");
                var titulo = System.Net.WebUtility.HtmlDecode(titleNode?.InnerText?.Trim() ?? "Unknown Title");

                string autor = ExtractAuthor(htmlDoc);

                string descricao = "Imported from mangalivre.blog";
                var descNode = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'synopsis') or contains(@class, 'description') or contains(@class, 'sinopse')]")
                            ?? htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'manga-summary')]//p");

                if (descNode == null)
                {
                    var heading = htmlDoc.DocumentNode.SelectSingleNode("//*[contains(text(), 'Sinopse') or contains(text(), 'Synopsis')]");
                    if (heading != null)
                        descNode = heading.SelectSingleNode("following-sibling::p | following-sibling::div");
                }

                if (descNode != null)
                {
                    descricao = System.Net.WebUtility.HtmlDecode(descNode.InnerText.Trim());
                    if (descricao.Length > 1000)
                        descricao = descricao[..1000] + "...";
                }

                return new Manga
                {
                    Titulo       = CleanTitle(titulo),
                    Autor        = autor,
                    Descricao    = descricao,
                    DataCriacao  = DateTime.Now,
                    ImagemCapa   = "/images/placeholder.jpg"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting metadata: {ex.Message}");
                return null;
            }
        }

        private static string ExtractAuthor(HtmlDocument htmlDoc)
        {
            var authorNode = htmlDoc.DocumentNode.SelectSingleNode(
                "//span[contains(text(), 'Autor') or contains(text(), 'Author')]/following-sibling::*")
                ?? htmlDoc.DocumentNode.SelectSingleNode(
                "//div[contains(@class, 'manga-info')]//a[contains(@href, 'autor') or contains(@href, 'author')]");

            if (authorNode != null)
                return System.Net.WebUtility.HtmlDecode(authorNode.InnerText.Trim());

            var infoNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'manga-tag') or contains(@class, 'info')]");
            if (infoNodes != null)
            {
                foreach (var node in infoNodes)
                {
                    var text = node.InnerText;
                    if (text.Contains("Autor") || text.Contains("Artist") || text.Contains("Author"))
                    {
                        var match = Regex.Match(text, @"(?:Autor|Author|Artist)[:\s]+(.+?)(?:\n|$)", RegexOptions.IgnoreCase);
                        if (match.Success) return match.Groups[1].Value.Trim();
                    }
                }
            }

            var globalMatch = Regex.Match(
                htmlDoc.DocumentNode.InnerText,
                @"(?:Autor|Author|Artist|Artista)[:\s]+([^\n\r]+)",
                RegexOptions.IgnoreCase);

            if (globalMatch.Success)
            {
                var found = globalMatch.Groups[1].Value.Trim();
                return found.Length > 100 ? found[..100] : found;
            }

            return "Unknown";
        }

        private static string? ExtractCoverUrl(HtmlDocument htmlDoc)
        {
            var coverNode = htmlDoc.DocumentNode.SelectSingleNode("//img[contains(@class, 'manga-cover')]")
                         ?? htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'manga-cover')]//img");

            if (coverNode != null)
            {
                var src = coverNode.GetAttributeValue("src", "");
                return string.IsNullOrEmpty(src) ? null : src;
            }

            var ogImage = htmlDoc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            var content = ogImage?.GetAttributeValue("content", "");
            return string.IsNullOrEmpty(content) ? null : content;
        }

        private List<ChapterInfo> ExtractChapterList(HtmlDocument htmlDoc)
        {
            var chapters = new List<ChapterInfo>();

            var chapterLinks = htmlDoc.DocumentNode.SelectNodes("//a[contains(@href, '/capitulo/')]")
                            ?? htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'chapter')]//a");

            if (chapterLinks == null) return chapters;

            var seenUrls = new HashSet<string>();

            foreach (var link in chapterLinks)
            {
                var href = link.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href) || !href.Contains("/capitulo/")) continue;

                href = NormalizeUrl(href) ?? href;
                if (!seenUrls.Add(href)) continue;

                string? chapterTitle = null;
                var linkText = System.Net.WebUtility.HtmlDecode(link.InnerText.Trim());
                var titleMatch = Regex.Match(linkText, @"Cap[ií]tulo\s+\d+\s*[-:]\s*(.+)", RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                    chapterTitle = titleMatch.Groups[1].Value.Trim();

                chapters.Add(new ChapterInfo
                {
                    Url    = href,
                    Number = ExtractChapterNumber(href, linkText),
                    Title  = chapterTitle
                });
            }

            return chapters;
        }

        private static string ExtractChapterNumber(string url, string linkText)
        {
            var urlMatch = Regex.Match(url, @"capitulo-(\d+(?:\.\d+)?)\/?$", RegexOptions.IgnoreCase);
            if (urlMatch.Success) return urlMatch.Groups[1].Value;

            var textMatch = Regex.Match(linkText, @"(?:Cap[ií]tulo|Cap\.?)\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (textMatch.Success) return textMatch.Groups[1].Value;

            return "0";
        }

        #endregion

        #region Private Methods - Chapter Filtering

        private List<ChapterInfo> FilterChaptersByRange(List<ChapterInfo> allChapters, string range)
        {
            if (range.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
                return allChapters;

            var selectedNumbers = ExpandChapterRange(range);
            if (selectedNumbers == null || selectedNumbers.Count == 0)
                return allChapters;

            return allChapters
                .Where(c => selectedNumbers.Any(n => Math.Abs(n - ParseChapterNumber(c.Number)) < 0.01m))
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
                        for (decimal n = Math.Ceiling(start); n <= Math.Floor(end); n++)
                            chapters.Add(n);
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

        #region Private Methods - Utilities

        private static string CleanTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "Unknown Manga";
            return (title.Length > 200 ? title[..200] : title).Trim();
        }

        #endregion
    }
}
