using AnyComic.Application.Common;
using AnyComic.Domain.Interfaces;
using AnyComic.Models;
using Microsoft.EntityFrameworkCore;

namespace AnyComic.Application.WeebCentral
{
    /// <summary>Progress snapshot of a catalog sweep, polled by the admin panel.</summary>
    public class SyncStatus
    {
        public bool Running { get; set; }
        public int Processed { get; set; }
        public int Total { get; set; }
        public int Errors { get; set; }
        public int NewSeries { get; set; }
        public int UpdatedSeries { get; set; }
        public int NewChapters { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
    }

    public interface ICatalogSyncService
    {
        /// <summary>Kicks off a sweep in the background. Returns false if one is already running.</summary>
        bool Start(int maxSeries, bool refreshExisting, string? proxyUrl);
        SyncStatus GetStatus();
    }

    /// <summary>
    /// Drives a background sweep of the WeebCentral catalog: enumerates series
    /// (most popular first), shallow-imports the ones not seen before (metadata +
    /// chapter list, no pages), and — when requested — revisits already-imported series
    /// to pull newly-released chapters. Tracks progress for the admin panel to poll.
    /// Runs fire-and-forget so the triggering request returns immediately. Only one sweep
    /// can run at a time.
    /// </summary>
    public class CatalogSyncService : ICatalogSyncService
    {
        private const int MaxParallelSeriesFetch = 3;
        private const string SourceName = "WeebCentral";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IWeebCentralScraperFactory _scraperFactory;
        private readonly object _statusLock = new();

        private bool _running;
        private int _processed;
        private int _total;
        private int _errors;
        private int _newSeries;
        private int _updatedSeries;
        private int _newChapters;
        private DateTime? _startedAt;
        private DateTime? _finishedAt;

        public CatalogSyncService(IServiceScopeFactory scopeFactory, IWeebCentralScraperFactory scraperFactory)
        {
            _scopeFactory = scopeFactory;
            _scraperFactory = scraperFactory;
        }

        public SyncStatus GetStatus()
        {
            lock (_statusLock)
            {
                return new SyncStatus
                {
                    Running       = _running,
                    Processed     = _processed,
                    Total         = _total,
                    Errors        = _errors,
                    NewSeries     = _newSeries,
                    UpdatedSeries = _updatedSeries,
                    NewChapters   = _newChapters,
                    StartedAt     = _startedAt,
                    FinishedAt    = _finishedAt
                };
            }
        }

        public bool Start(int maxSeries, bool refreshExisting, string? proxyUrl)
        {
            lock (_statusLock)
            {
                if (_running) return false;
                _running       = true;
                _processed     = 0;
                _total         = 0;
                _errors        = 0;
                _newSeries     = 0;
                _updatedSeries = 0;
                _newChapters   = 0;
                _startedAt     = DateTime.Now;
                _finishedAt    = null;
            }

            _ = Task.Run(() => RunSyncAsync(maxSeries, refreshExisting, proxyUrl));
            return true;
        }

        private async Task RunSyncAsync(int maxSeries, bool refreshExisting, string? proxyUrl)
        {
            var scraper = _scraperFactory.Create(proxyUrl);

            try
            {
                var catalog = await scraper.EnumerateCatalog(maxSeries);

                Dictionary<string, int> existingByFonteId;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                    existingByFonteId = await db.Mangas
                        .Where(m => m.Fonte == SourceName && m.FonteId != null)
                        .ToDictionaryAsync(m => m.FonteId!, m => m.Id);
                }

                var newEntries = catalog.Where(e => !existingByFonteId.ContainsKey(e.SeriesId)).ToList();
                var existingEntries = refreshExisting
                    ? catalog.Where(e => existingByFonteId.ContainsKey(e.SeriesId)).ToList()
                    : new List<CatalogEntry>();

                lock (_statusLock) { _total = newEntries.Count + existingEntries.Count; }

                using var httpThrottle = new SemaphoreSlim(MaxParallelSeriesFetch, MaxParallelSeriesFetch);
                using var dbWriteLock = new SemaphoreSlim(1, 1);

                var newTasks = newEntries.Select(entry =>
                    ProcessNewSeriesAsync(scraper, entry, httpThrottle, dbWriteLock));

                var updateTasks = existingEntries.Select(entry =>
                    ProcessExistingSeriesAsync(scraper, entry, existingByFonteId[entry.SeriesId],
                        httpThrottle, dbWriteLock));

                await Task.WhenAll(newTasks.Concat(updateTasks));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CatalogSync] Sweep failed: {ex.Message}");
            }
            finally
            {
                lock (_statusLock)
                {
                    _running    = false;
                    _finishedAt = DateTime.Now;
                }
            }
        }

        /// <summary>Shallow-imports a brand-new series (metadata + chapter list, no pages).</summary>
        private async Task ProcessNewSeriesAsync(
            IWeebCentralScraper scraper,
            CatalogEntry entry,
            SemaphoreSlim httpThrottle,
            SemaphoreSlim dbWriteLock)
        {
            await httpThrottle.WaitAsync();
            try
            {
                // Request pacing/backoff for rate limits is handled centrally inside
                // the scraper (shared across all concurrent fetches).
                var result = await scraper.ImportSeriesShallow(entry.Url);

                if (result == null)
                {
                    lock (_statusLock) { _errors++; }
                    return;
                }

                var (manga, chapters) = result.Value;

                await dbWriteLock.WaitAsync();
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                    db.Mangas.Add(manga);
                    await db.SaveChangesAsync();

                    foreach (var chapterData in chapters)
                    {
                        db.Capitulos.Add(BuildChapter(manga.Id, chapterData));
                    }

                    await db.SaveChangesAsync();
                }
                finally
                {
                    dbWriteLock.Release();
                }

                lock (_statusLock) { _newSeries++; _newChapters += chapters.Count; }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CatalogSync] Error processing {entry.Url}: {ex.Message}");
                lock (_statusLock) { _errors++; }
            }
            finally
            {
                lock (_statusLock) { _processed++; }
                httpThrottle.Release();
            }
        }

        /// <summary>
        /// Revisits a series already in the DB and inserts only chapters not yet present
        /// (matched by <c>FonteCapituloId</c>), so newly-released chapters are pulled in.
        /// </summary>
        private async Task ProcessExistingSeriesAsync(
            IWeebCentralScraper scraper,
            CatalogEntry entry,
            int mangaId,
            SemaphoreSlim httpThrottle,
            SemaphoreSlim dbWriteLock)
        {
            await httpThrottle.WaitAsync();
            try
            {
                var result = await scraper.ImportSeriesShallow(entry.Url);

                if (result == null)
                {
                    lock (_statusLock) { _errors++; }
                    return;
                }

                var (_, chapters) = result.Value;

                await dbWriteLock.WaitAsync();
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                    var existingChapterIds = await db.Capitulos
                        .Where(c => c.MangaId == mangaId && c.FonteCapituloId != null)
                        .Select(c => c.FonteCapituloId!)
                        .ToListAsync();
                    var existingSet = new HashSet<string>(existingChapterIds);

                    var missing = chapters
                        .Where(c => !string.IsNullOrEmpty(c.FonteCapituloId)
                                    && !existingSet.Contains(c.FonteCapituloId!))
                        .ToList();

                    if (missing.Count > 0)
                    {
                        foreach (var chapterData in missing)
                        {
                            db.Capitulos.Add(BuildChapter(mangaId, chapterData));
                        }

                        await db.SaveChangesAsync();

                        lock (_statusLock) { _updatedSeries++; _newChapters += missing.Count; }
                    }
                }
                finally
                {
                    dbWriteLock.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CatalogSync] Error refreshing {entry.Url}: {ex.Message}");
                lock (_statusLock) { _errors++; }
            }
            finally
            {
                lock (_statusLock) { _processed++; }
                httpThrottle.Release();
            }
        }

        private static Capitulo BuildChapter(int mangaId, ChapterImportData chapterData)
        {
            if (!decimal.TryParse(chapterData.ChapterNumber, out var chapterNum))
                chapterNum = 0;

            return new Capitulo
            {
                MangaId         = mangaId,
                NumeroCapitulo  = (int)Math.Floor(chapterNum),
                NomeCapitulo    = chapterData.ChapterTitle,
                FonteCapituloId = chapterData.FonteCapituloId,
                DataCriacao     = DateTime.Now
            };
        }
    }
}
