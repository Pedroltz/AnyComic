using Microsoft.AspNetCore.Mvc;

namespace AnyComic.Controllers;

/// <summary>
/// Proxies external images that require a trusted Referer header (e.g. MangaDex CDN).
/// Requests go: browser → /Proxy/Image?url=... → external CDN → browser.
/// Only URLs from whitelisted domains are allowed.
/// </summary>
public class ProxyController : Controller
{
    private static readonly HttpClient _http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    });

    private static readonly HashSet<string> _allowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "uploads.mangadex.org"
    };

    [HttpGet]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Image(string url)
    {
        if (string.IsNullOrEmpty(url))
            return BadRequest();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http")
            || !_allowedHosts.Contains(uri.Host))
        {
            return BadRequest();
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Referrer = new Uri("https://mangadex.org/");
            request.Headers.Add("User-Agent", "Mozilla/5.0");

            var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var stream = await response.Content.ReadAsStreamAsync();

            return File(stream, contentType);
        }
        catch
        {
            return StatusCode(502);
        }
    }
}
