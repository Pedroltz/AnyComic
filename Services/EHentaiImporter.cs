using HtmlAgilityPack;
using AnyComic.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AnyComic.Services
{
    /// <summary>
    /// Imports manga metadata and page image URLs from e-hentai.org.
    /// Images are NOT downloaded — the actual image URL is extracted from each
    /// viewer page and stored directly, so the reader loads on demand.
    /// </summary>
    public class EHentaiImporter
    {
        private readonly HttpClient _httpClient;

        public EHentaiImporter()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        /// <summary>
        /// Indexes a manga from e-hentai.org. Fetches metadata and page image URLs;
        /// no images are downloaded or stored locally.
        /// </summary>
        public async Task<(Manga manga, List<string> pageUrls)?> ImportFromUrl(string url)
        {
            try
            {
                if (!IsValidEHentaiUrl(url))
                    return null;

                var html = await _httpClient.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var manga = ExtractMangaMetadata(htmlDoc);
                if (manga == null)
                    return null;

                // Collect all viewer page links (with gallery pagination)
                var viewerPageUrls = await ExtractPageUrls(htmlDoc, url);
                if (viewerPageUrls.Count == 0)
                    return null;

                // Visit each viewer page to extract the actual image URL
                var imageUrls = await IndexPages(viewerPageUrls);

                // Use first page as cover if available
                if (imageUrls.Count > 0)
                    manga.ImagemCapa = imageUrls[0];

                return (manga, imageUrls);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing from e-hentai: {ex.Message}");
                return null;
            }
        }

        #region Private Methods - Indexing

        /// <summary>
        /// Visits each viewer page URL and extracts the actual image URL.
        /// E-Hentai viewer pages have an <img id="img"> with the real CDN URL.
        /// </summary>
        private async Task<List<string>> IndexPages(List<string> viewerPageUrls)
        {
            var imageUrls = new List<string>();
            int pageNumber = 1;

            foreach (var pageUrl in viewerPageUrls)
            {
                try
                {
                    var pageHtml = await _httpClient.GetStringAsync(pageUrl);
                    var pageDoc = new HtmlDocument();
                    pageDoc.LoadHtml(pageHtml);

                    var imageNode = pageDoc.DocumentNode.SelectSingleNode("//img[@id='img']")
                                 ?? pageDoc.DocumentNode.SelectSingleNode("//div[@id='i3']//img");

                    if (imageNode != null)
                    {
                        var imageUrl = imageNode.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(imageUrl))
                            imageUrls.Add(imageUrl);
                    }

                    pageNumber++;
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error indexing page {pageNumber}: {ex.Message}");
                }
            }

            Console.WriteLine($"Indexed {imageUrls.Count} pages");
            return imageUrls;
        }

        #endregion

        #region Private Methods - Metadata Extraction

        private static bool IsValidEHentaiUrl(string url)
            => !string.IsNullOrEmpty(url)
               && (url.Contains("e-hentai.org/g/") || url.Contains("exhentai.org/g/"));

        private Manga? ExtractMangaMetadata(HtmlDocument htmlDoc)
        {
            try
            {
                var titleNode = htmlDoc.DocumentNode.SelectSingleNode("//h1[@id='gn']")
                             ?? htmlDoc.DocumentNode.SelectSingleNode("//h1");
                var titulo = titleNode?.InnerText?.Trim() ?? "Unknown Title";

                var artistNode = htmlDoc.DocumentNode.SelectSingleNode("//a[contains(@href, 'artist:')]");
                var autor = artistNode?.InnerText?.Trim() ?? "Unknown";

                var dateNode = htmlDoc.DocumentNode.SelectSingleNode("//td[@class='gdt2' and contains(., '-')]");
                DateTime dataCriacao = DateTime.Now;

                if (dateNode != null
                    && DateTime.TryParseExact(dateNode.InnerText.Trim(), "yyyy-MM-dd HH:mm",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    dataCriacao = parsedDate;
                }

                return new Manga
                {
                    Titulo      = CleanTitle(titulo),
                    Autor       = autor,
                    Descricao   = "Imported from e-hentai.org",
                    DataCriacao = dataCriacao,
                    ImagemCapa  = "/images/placeholder.jpg"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting metadata: {ex.Message}");
                return null;
            }
        }

        private async Task<List<string>> ExtractPageUrls(HtmlDocument htmlDoc, string baseUrl)
        {
            var pageUrls = new List<string>();

            try
            {
                var cleanBaseUrl = baseUrl.Split('?')[0];
                ExtractPageUrlsFromDocument(htmlDoc, pageUrls);

                var paginationNode = htmlDoc.DocumentNode.SelectSingleNode("//table[@class='ptt']");
                if (paginationNode != null)
                {
                    var pageLinks = paginationNode.SelectNodes(".//a");
                    if (pageLinks != null)
                    {
                        var pageNumbers = new HashSet<int>();
                        foreach (var link in pageLinks)
                        {
                            var href  = link.GetAttributeValue("href", "");
                            var match = Regex.Match(href, @"\?p=(\d+)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int pageNum))
                                pageNumbers.Add(pageNum);
                        }

                        foreach (var pageNum in pageNumbers.OrderBy(p => p))
                        {
                            try
                            {
                                var pageHtml = await _httpClient.GetStringAsync($"{cleanBaseUrl}?p={pageNum}");
                                var pageDoc  = new HtmlDocument();
                                pageDoc.LoadHtml(pageHtml);
                                ExtractPageUrlsFromDocument(pageDoc, pageUrls);
                                await Task.Delay(500);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error fetching gallery page {pageNum}: {ex.Message}");
                            }
                        }
                    }
                }

                Console.WriteLine($"Total viewer pages found: {pageUrls.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting page URLs: {ex.Message}");
            }

            return pageUrls;
        }

        private static void ExtractPageUrlsFromDocument(HtmlDocument htmlDoc, List<string> pageUrls)
        {
            var pageLinks = htmlDoc.DocumentNode.SelectNodes("//div[@class='gdtm']//a")
                         ?? htmlDoc.DocumentNode.SelectNodes("//div[@id='gdt']//a");

            if (pageLinks == null) return;

            foreach (var link in pageLinks)
            {
                var href = link.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href) && !pageUrls.Contains(href))
                    pageUrls.Add(href);
            }
        }

        #endregion

        #region Private Methods - Utilities

        private static string CleanTitle(string title)
        {
            title = Regex.Replace(title, @"^\([^)]+\)\s*", "");
            title = Regex.Replace(title, @"^\[[^\]]+\]\s*", "");
            title = Regex.Replace(title, @"\s*\[[^\]]*(?:English|Portuguese|Spanish|Chinese|Korean|Japanese)[^\]]*\]\s*$", "", RegexOptions.IgnoreCase);
            return title.Trim();
        }

        #endregion
    }
}
