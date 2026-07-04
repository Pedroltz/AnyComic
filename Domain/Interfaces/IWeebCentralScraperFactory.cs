namespace AnyComic.Domain.Interfaces;

/// <summary>
/// Creates <see cref="IWeebCentralScraper"/> instances. A factory (rather than a single
/// injected instance) is needed because the proxy is chosen at runtime per operation.
/// </summary>
public interface IWeebCentralScraperFactory
{
    IWeebCentralScraper Create(string? proxyUrl = null);
}
