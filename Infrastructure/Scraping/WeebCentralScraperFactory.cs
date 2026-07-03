using AnyComic.Domain.Interfaces;

namespace AnyComic.Infrastructure.Scraping;

/// <summary>
/// Default factory: builds a <see cref="WeebCentralImporter"/> configured with the given proxy.
/// Isolates the single remaining <c>new WeebCentralImporter(...)</c> behind the port.
/// </summary>
public class WeebCentralScraperFactory : IWeebCentralScraperFactory
{
    public IWeebCentralScraper Create(string? proxyUrl = null)
        => new WeebCentralImporter(string.IsNullOrEmpty(proxyUrl) ? null : proxyUrl);
}
