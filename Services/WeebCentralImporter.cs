using AnyComic.Models;
using HtmlAgilityPack;
using System.Net;
using System.Text.RegularExpressions;

namespace AnyComic.Services
{
    /// <summary>
    /// Imports manga metadata and chapter page URLs from weebcentral.com.
    /// Images are NOT downloaded — external URLs are indexed and stored directly.
    /// WeebCentral uses HTMX for dynamic content; sub-requests need HX-Request headers.
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

            // Only set headers that are the same for every request type
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
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
            public string       ChapterNumber    { get; set; } = string.Empty;
            public string?      ChapterTitle     { get; set; }
            /// <summary>External image URLs from the source site (no local copies).</summary>
            public List<string> PageUrls         { get; set; } = new();
            /// <summary>Chapter ID on the source site, used for lazy page indexing later.</summary>
            public string?      FonteCapituloId  { get; set; }
        }

        /// <summary>A series found while enumerating the WeebCentral catalog.</summary>
        public record CatalogEntry(string SeriesId, string Url, string Title, string? CoverUrl);

        #endregion

        #region Public Methods

        public async Task<(Manga manga, List<ChapterImportData> chapters)?> ImportFromUrl(
            string url,
            string chapterRange = "all")
        {
            try
            {
                var metadata = await FetchSeriesMetadata(url);
                if (metadata == null) return null;

                var (_, manga, allChapters) = metadata.Value;

                var selectedChapters = FilterChaptersByRange(allChapters, chapterRange);
                Console.WriteLine($"Indexing {selectedChapters.Count} chapters...");

                var importedChapters = new List<ChapterImportData>();
                int index = 1;

                foreach (var chapter in selectedChapters.OrderBy(c => c.ChapterNumber))
                {
                    try
                    {
                        Console.WriteLine($"Indexing chapter {index}/{selectedChapters.Count}: {chapter.ChapterNumber}");

                        var pageUrls = await IndexChapterPages(chapter, url);

                        if (pageUrls.Count > 0)
                        {
                            importedChapters.Add(new ChapterImportData
                            {
                                ChapterNumber   = chapter.ChapterNumber.ToString(),
                                ChapterTitle    = string.IsNullOrEmpty(chapter.ChapterTitle) ? null : chapter.ChapterTitle,
                                PageUrls        = pageUrls,
                                FonteCapituloId = chapter.Id
                            });
                            Console.WriteLine($"  Indexed {pageUrls.Count} pages");
                        }
                        else
                        {
                            Console.WriteLine($"  No pages found for chapter {chapter.ChapterNumber}");
                        }

                        index++;
                        await Task.Delay(600);
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

        /// <summary>
        /// Shallow import: fetches series metadata and the full chapter list, but does NOT
        /// index any chapter pages. Used by the catalog sweep — pages are indexed lazily
        /// the first time a reader opens a chapter (see <see cref="IndexChapterPages"/>).
        /// </summary>
        public async Task<(Manga manga, List<ChapterImportData> chapters)?> ImportSeriesShallow(string url)
        {
            try
            {
                var metadata = await FetchSeriesMetadata(url);
                if (metadata == null) return null;

                var (_, manga, allChapters) = metadata.Value;

                var chapters = allChapters
                    .OrderBy(c => c.ChapterNumber)
                    .Select(c => new ChapterImportData
                    {
                        ChapterNumber   = c.ChapterNumber.ToString(),
                        ChapterTitle    = string.IsNullOrEmpty(c.ChapterTitle) ? null : c.ChapterTitle,
                        FonteCapituloId = c.Id,
                        PageUrls        = new List<string>()
                    })
                    .ToList();

                return (manga, chapters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error shallow-importing from WeebCentral: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Pages the WeebCentral search endpoint (popularity-sorted) to enumerate series for
        /// a catalog sweep. Stops once <paramref name="maxSeries"/> is reached or a page
        /// returns no new series. Selectors are exploratory — see console output on first run.
        /// </summary>
        public async Task<List<CatalogEntry>> EnumerateCatalog(int maxSeries)
        {
            var results = new List<CatalogEntry>();
            var seenIds = new HashSet<string>();
            const int pageSize = 32;
            var referrer = $"{BASE_URL}/";

            for (int offset = 0; results.Count < maxSeries; offset += pageSize)
            {
                try
                {
                    var searchUrl = $"{BASE_URL}/search/data?limit={pageSize}&offset={offset}" +
                                     "&sort=Popularity&order=Descending&official=Any&display_mode=Full+Display";
                    var html = await GetHtmx(searchUrl, referrer);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    Console.WriteLine($"  [catalog] offset={offset} HTML length: {html.Length} chars");

                    var seriesLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/series/')]");
                    if (seriesLinks == null)
                    {
                        Console.WriteLine("  [catalog] No series links found — stopping");
                        break;
                    }

                    int newOnThisPage = 0;

                    foreach (var link in seriesLinks)
                    {
                        var href = link.GetAttributeValue("href", "");
                        var match = Regex.Match(href, @"/series/([A-Z0-9]+)(?:/([^/?""']+))?", RegexOptions.IgnoreCase);
                        if (!match.Success) continue;

                        var seriesId = match.Groups[1].Value;
                        if (!seenIds.Add(seriesId)) continue;

                        var slug = match.Groups[2].Success ? match.Groups[2].Value : "series";
                        var absoluteUrl = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            ? href
                            : $"{BASE_URL}/series/{seriesId}/{slug}";

                        var title = HtmlEntity.DeEntitize(link.GetAttributeValue("title", "").Trim());
                        if (string.IsNullOrEmpty(title))
                            title = HtmlEntity.DeEntitize(link.InnerText.Trim());

                        var coverImg = link.SelectSingleNode(".//img") ?? link.ParentNode?.SelectSingleNode(".//img");
                        var coverUrlRaw = coverImg?.GetAttributeValue("src", "");
                        var coverUrl = string.IsNullOrEmpty(coverUrlRaw) ? null : coverUrlRaw;

                        results.Add(new CatalogEntry(seriesId, absoluteUrl, title, coverUrl));
                        newOnThisPage++;

                        if (results.Count >= maxSeries) break;
                    }

                    Console.WriteLine($"  [catalog] offset={offset} found {newOnThisPage} new series (total {results.Count})");

                    if (newOnThisPage == 0) break;

                    await Task.Delay(400);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [catalog] Error at offset {offset}: {ex.Message}");
                    break;
                }
            }

            return results;
        }

        #endregion

        #region Private Methods - Shared Series Fetch

        /// <summary>
        /// Fetches series metadata (title, author, cover, description, year) and the full
        /// chapter list. Shared by <see cref="ImportFromUrl"/> and <see cref="ImportSeriesShallow"/>.
        /// </summary>
        private async Task<(string seriesId, Manga manga, List<WeebCentralChapter> chapters)?> FetchSeriesMetadata(string url)
        {
            var seriesId = ExtractSeriesId(url);
            if (seriesId == null)
            {
                Console.WriteLine("Invalid WeebCentral URL");
                return null;
            }

            // Fetch manga page as a normal browser navigation
            var mangaHtml = await GetNavigate(url);
            var mangaDoc = new HtmlDocument();
            mangaDoc.LoadHtml(mangaHtml);

            var title       = ExtractTitle(mangaDoc) ?? "Unknown Manga";
            var coverUrl    = ExtractCoverUrl(mangaDoc);
            var detailMap   = ExtractDetailMap(mangaDoc);
            var author      = ExtractAuthor(mangaDoc, detailMap)             ?? "Unknown";
            var year        = ExtractYear(mangaDoc, detailMap);
            var description = ExtractDescription(mangaDoc, detailMap) ?? "Imported from WeebCentral";

            Console.WriteLine($"Found manga: {title} by {author} ({year?.ToString() ?? "no year"})");

            var allChapters = await GetFullChapterList(seriesId, url);
            if (allChapters.Count == 0)
            {
                Console.WriteLine("No chapters found");
                return null;
            }

            Console.WriteLine($"Found {allChapters.Count} chapters");

            var dataCriacao = year.HasValue
                ? new DateTime(year.Value, 1, 1)
                : DateTime.Now;

            var manga = new Manga
            {
                Titulo      = CleanTitle(title),
                Autor       = author,
                Descricao   = CleanDescription(description),
                DataCriacao = dataCriacao,
                ImagemCapa  = coverUrl ?? "/images/placeholder.jpg",
                Fonte       = "WeebCentral",
                FonteId     = seriesId
            };

            return (seriesId, manga, allChapters);
        }

        #endregion

        #region Private Methods - HTTP Helpers

        // Every actual HTTP request to weebcentral.com goes through SendThrottledAsync, which
        // enforces a minimum gap between requests (regardless of how many callers are racing
        // concurrently, e.g. the catalog sweep's parallel series fetches) and retries with
        // backoff on 429/503. Without this, bursts of concurrent requests (catalog sweeps of
        // 100+ series) get rate-limited by the site.
        private readonly SemaphoreSlim _requestGate = new(1, 1);
        private DateTime _lastRequestAtUtc = DateTime.MinValue;
        private static readonly TimeSpan MinRequestInterval = TimeSpan.FromMilliseconds(500);
        private const int MaxRetriesOnRateLimit = 4;

        /// <summary>
        /// Sends a request, waiting out a minimum gap since the last request and retrying
        /// with backoff if the server responds 429 (Too Many Requests) or 503.
        /// </summary>
        private async Task<HttpResponseMessage> SendThrottledAsync(Func<HttpRequestMessage> buildRequest)
        {
            for (int attempt = 0; ; attempt++)
            {
                await _requestGate.WaitAsync();
                try
                {
                    var wait = MinRequestInterval - (DateTime.UtcNow - _lastRequestAtUtc);
                    if (wait > TimeSpan.Zero)
                        await Task.Delay(wait);
                    _lastRequestAtUtc = DateTime.UtcNow;
                }
                finally
                {
                    _requestGate.Release();
                }

                var response = await _httpClient.SendAsync(buildRequest());

                var statusCode = (int)response.StatusCode;
                if (statusCode != 429 && statusCode != 503)
                    return response;

                if (attempt >= MaxRetriesOnRateLimit)
                    return response; // give up — caller's EnsureSuccessStatusCode() will throw

                var backoff = response.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                Console.WriteLine($"  Rate limited ({statusCode}) — retrying in {backoff.TotalSeconds:0}s (attempt {attempt + 1}/{MaxRetriesOnRateLimit})...");
                response.Dispose();
                await Task.Delay(backoff);
            }
        }

        /// <summary>
        /// Fetches a URL as a standard browser page navigation.
        /// </summary>
        private async Task<string> GetNavigate(string url)
        {
            var response = await SendThrottledAsync(() =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                req.Headers.Add("Sec-Fetch-Dest", "document");
                req.Headers.Add("Sec-Fetch-Mode", "navigate");
                req.Headers.Add("Sec-Fetch-Site", "none");
                req.Headers.Add("Sec-Fetch-User", "?1");
                req.Headers.Add("Upgrade-Insecure-Requests", "1");
                return req;
            });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Fetches a URL as an HTMX partial request (same-origin XHR).
        /// WeebCentral's chapter list and image endpoints are loaded this way.
        /// </summary>
        private async Task<string> GetHtmx(string url, string referrerUrl)
        {
            var response = await SendThrottledAsync(() =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Accept", "*/*");
                req.Headers.Add("Sec-Fetch-Dest", "empty");
                req.Headers.Add("Sec-Fetch-Mode", "cors");
                req.Headers.Add("Sec-Fetch-Site", "same-origin");
                req.Headers.Referrer = new Uri(referrerUrl);
                req.Headers.Add("HX-Request", "true");
                req.Headers.Add("HX-Current-URL", referrerUrl);
                return req;
            });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        #endregion

        #region Private Methods - Indexing

        /// <summary>
        /// Fetches the page image URLs for a chapter. Public so it can be reused for lazy
        /// page indexing (<c>MangaController.Read</c>) when a chapter imported by the catalog
        /// sweep is opened for the first time. Note: <paramref name="mangaUrl"/> is unused —
        /// only <c>chapter.Id</c> matters, kept for call-site clarity.
        /// </summary>
        public async Task<List<string>> IndexChapterPages(WeebCentralChapter chapter, string mangaUrl)
        {
            var pageUrls = new List<string>();

            try
            {
                var chapterPageUrl = $"{BASE_URL}/chapters/{chapter.Id}";
                var imagesUrl = $"{chapterPageUrl}/images?is_prev=False&current_page=1&reading_style=long_strip";
                var html = await GetHtmx(imagesUrl, chapterPageUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var imgNodes = doc.DocumentNode.SelectNodes("//img[@alt]")
                            ?? doc.DocumentNode.SelectNodes("//img");

                if (imgNodes == null) return pageUrls;

                foreach (var img in imgNodes)
                {
                    var alt = img.GetAttributeValue("alt", "");
                    if (!alt.StartsWith("Page ", StringComparison.OrdinalIgnoreCase)) continue;

                    var src = img.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(src))
                        pageUrls.Add(src);
                }

                // Fallback: any image with a recognisable extension if the above matched nothing
                if (pageUrls.Count == 0 && imgNodes != null)
                {
                    foreach (var img in imgNodes)
                    {
                        var src = img.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(src) &&
                            (src.Contains(".png") || src.Contains(".jpg") || src.Contains(".webp") || src.Contains(".jpeg")))
                        {
                            pageUrls.Add(src);
                        }
                    }
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

        private static string? ExtractSeriesId(string url)
        {
            var match = Regex.Match(url, @"weebcentral\.com/series/([A-Z0-9]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string? ExtractTitle(HtmlDocument doc)
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

        private static string? ExtractAuthor(HtmlDocument doc, Dictionary<string, string>? detailMap = null)
        {
            detailMap ??= ExtractDetailMap(doc);
            foreach (var label in new[] { "Author(s)", "Author", "Artist(s)", "Artist" })
                if (detailMap.TryGetValue(label, out var val)) return val;
            return null;
        }

        /// <summary>
        /// Builds a label→value dictionary from WeebCentral's metadata section.
        /// Tag-agnostic: reads direct element children of each <li> or flex container,
        /// treating the first child's text as label and the rest as value.
        /// </summary>
        private static Dictionary<string, string> ExtractDetailMap(HtmlDocument doc)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Dump body text preview to console to help identify correct selectors
            var bodyText = doc.DocumentNode.SelectSingleNode("//body")?.InnerText ?? "";
            bodyText = Regex.Replace(bodyText, @"\s+", " ").Trim();
            Console.WriteLine($"  [meta] Body preview: {bodyText[..Math.Min(500, bodyText.Length)]}");

            var candidates = doc.DocumentNode.SelectNodes("//li");

            if (candidates != null)
            {
                foreach (var node in candidates)
                {
                    // Read text of each direct element child (tag-agnostic)
                    var childTexts = node.ChildNodes
                        .Where(c => c.NodeType == HtmlAgilityPack.HtmlNodeType.Element)
                        .Select(c => HtmlEntity.DeEntitize(c.InnerText.Trim()))
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();

                    if (childTexts.Count < 2) continue;

                    // WeebCentral labels end with ": " (e.g. "Author(s): ") — normalise
                    var label = childTexts[0].TrimEnd(':', ' ');
                    if (label.Length > 40 || label.Contains('.')) continue;

                    var value = string.Join(", ", childTexts.Skip(1));
                    if (!map.ContainsKey(label) && !string.IsNullOrEmpty(value))
                        map[label] = value;
                }
            }

            // Fallback XPath: find exact label text anywhere, then grab next element sibling
            foreach (var label in new[] { "Author(s)", "Author", "Artist(s)", "Year", "Published" })
            {
                if (map.ContainsKey(label)) continue;

                var labelNode = doc.DocumentNode.SelectSingleNode(
                    $"//*[normalize-space(text())='{label}']");
                if (labelNode == null) continue;

                // Next element sibling of the label node itself
                var sibling = labelNode.NextSibling;
                while (sibling != null && sibling.NodeType != HtmlAgilityPack.HtmlNodeType.Element)
                    sibling = sibling.NextSibling;

                // Or parent's next element sibling
                if (sibling == null)
                {
                    sibling = labelNode.ParentNode?.NextSibling;
                    while (sibling != null && sibling.NodeType != HtmlAgilityPack.HtmlNodeType.Element)
                        sibling = sibling.NextSibling;
                }

                if (sibling != null)
                {
                    var val = HtmlEntity.DeEntitize(sibling.InnerText.Trim());
                    if (!string.IsNullOrEmpty(val))
                        map[label] = val;
                }
            }

            Console.WriteLine($"  [meta] Detail map: {string.Join(" | ", map.Select(kv => $"{kv.Key}={kv.Value}"))}");
            return map;
        }

        private static string? FindDetailValue(Dictionary<string, string> map, params string[] labels)
        {
            foreach (var label in labels)
                if (map.TryGetValue(label, out var val)) return val;
            return null;
        }

        private static string? ExtractDescription(HtmlDocument doc, Dictionary<string, string>? detailMap = null)
        {
            detailMap ??= ExtractDetailMap(doc);

            // 1. "Description" field from the detail map — WeebCentral puts the real
            //    synopsis inside <li><strong>Description</strong><p>...</p></li>
            if (detailMap.TryGetValue("Description", out var mapDesc) && mapDesc.Length > 20)
            {
                Console.WriteLine($"  [meta] Description from detail map ({mapDesc.Length} chars)");
                return mapDesc;
            }

            // 2. og:description — only if it's not the generic "Read X online" placeholder
            var ogDesc = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']");
            if (ogDesc != null)
            {
                var content = ogDesc.GetAttributeValue("content", "");
                if (!string.IsNullOrEmpty(content)
                    && !content.Contains("online for free", StringComparison.OrdinalIgnoreCase)
                    && !content.Contains("Read ") )
                {
                    Console.WriteLine($"  [meta] Description from og:description ({content.Length} chars)");
                    return HtmlEntity.DeEntitize(content);
                }
            }

            // 2. Any <p> inside a section/div that looks like a synopsis
            var synopsisSelectors = new[]
            {
                "//section[contains(@class,'description')]//p",
                "//div[contains(@class,'description')]//p",
                "//section[contains(@class,'synopsis')]//p",
                "//div[contains(@class,'synopsis')]//p",
                "//p[contains(@class,'description')]",
                "//p[contains(@class,'synopsis')]",
                // WeebCentral uses x-show for collapsible descriptions
                "//*[@x-show]//p",
                "//article//section//p",
                "//main//p[string-length(.) > 50]",
            };

            foreach (var selector in synopsisSelectors)
            {
                var nodes = doc.DocumentNode.SelectNodes(selector);
                if (nodes == null) continue;
                var text = string.Join(" ", nodes.Select(n => HtmlEntity.DeEntitize(n.InnerText.Trim())))
                                .Trim();
                if (text.Length > 20)
                {
                    Console.WriteLine($"  [meta] Description from selector '{selector}' ({text.Length} chars)");
                    return text;
                }
            }

            Console.WriteLine("  [meta] Description not found");
            return null;
        }

        private static int? ExtractYear(HtmlDocument doc, Dictionary<string, string>? detailMap = null)
        {
            detailMap ??= ExtractDetailMap(doc);
            // WeebCentral uses "Released" (colon already stripped by ExtractDetailMap)
            var yearStr = FindDetailValue(detailMap, "Released", "Year", "Published", "Release Year", "Release");
            if (yearStr != null)
            {
                var m = Regex.Match(yearStr, @"\b(19|20)\d{2}\b");
                if (m.Success && int.TryParse(m.Value, out int y)) return y;
            }

            // Fallback: any 4-digit year in the page text
            var anyYear = doc.DocumentNode.SelectSingleNode(
                "//span[contains(@class,'year')] | //td[contains(@class,'year')] | //li[contains(.,'Year')]");
            if (anyYear != null)
            {
                var m = Regex.Match(anyYear.InnerText, @"\b(19|20)\d{2}\b");
                if (m.Success && int.TryParse(m.Value, out int y)) return y;
            }

            return null;
        }

        private static string? ExtractCoverUrl(HtmlDocument doc)
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

        private async Task<List<WeebCentralChapter>> GetFullChapterList(string seriesId, string mangaUrl)
        {
            var chapters = new List<WeebCentralChapter>();

            try
            {
                var listUrl = $"{BASE_URL}/series/{seriesId}/full-chapter-list";
                // The full-chapter-list endpoint is loaded via HTMX from the series page
                var html = await GetHtmx(listUrl, mangaUrl);
                var doc  = new HtmlDocument();
                doc.LoadHtml(html);

                Console.WriteLine($"  Chapter list HTML length: {html.Length} chars");

                // Primary selector: anchor tags that link to a chapter
                var chapterLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/chapters/')]")
                                ?? doc.DocumentNode.SelectNodes("//a[contains(@href, 'chapter')]");

                if (chapterLinks == null)
                {
                    Console.WriteLine("  No chapter links found in HTML");
                    return chapters;
                }

                foreach (var link in chapterLinks)
                {
                    var href = link.GetAttributeValue("href", "");
                    if (string.IsNullOrEmpty(href)) continue;

                    // Accept both relative and absolute URLs
                    if (!href.Contains("/chapters/")) continue;

                    var chapterText = HtmlEntity.DeEntitize(link.InnerText.Trim());

                    // Extract chapter number — try several patterns
                    decimal chapterNum = 0;
                    var numMatch = Regex.Match(chapterText, @"(?:Chapter|Ch\.?)\s*([\d.]+)", RegexOptions.IgnoreCase);
                    if (numMatch.Success)
                    {
                        decimal.TryParse(numMatch.Groups[1].Value,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out chapterNum);
                    }
                    else
                    {
                        // Fallback: first number found anywhere in the text
                        var anyNum = Regex.Match(chapterText, @"([\d]+(?:\.[\d]+)?)");
                        if (anyNum.Success)
                            decimal.TryParse(anyNum.Groups[1].Value,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out chapterNum);
                    }

                    // Extract chapter ID from URL
                    var idMatch = Regex.Match(href, @"/chapters/([A-Z0-9]+)", RegexOptions.IgnoreCase);
                    if (!idMatch.Success) continue;

                    var titleMatch = Regex.Match(chapterText, @"(?:Chapter|Ch\.?)\s*[\d.]+\s*[:\-]\s*(.+)", RegexOptions.IgnoreCase);

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

                Console.WriteLine($"  Parsed {chapters.Count} chapters");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching chapter list: {ex.Message}");
            }

            return chapters;
        }

        #endregion

        #region Private Methods - Chapter Filtering

        private static List<WeebCentralChapter> FilterChaptersByRange(List<WeebCentralChapter> allChapters, string range)
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
