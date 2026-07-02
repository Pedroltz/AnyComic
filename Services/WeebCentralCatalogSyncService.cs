using AnyComic.Data;
using AnyComic.Models;
using Microsoft.EntityFrameworkCore;

namespace AnyComic.Services
{
    /// <summary>
    /// Drives a background sweep of the WeebCentral catalog: enumerates series
    /// (most popular first), shallow-imports the ones not seen before (metadata +
    /// chapter list, no pages), and tracks progress for the admin panel to poll.
    /// Runs fire-and-forget from <c>AdminController.SyncWeebCentralCatalog</c> so the
    /// triggering request returns immediately. Only one sweep can run at a time.
    /// </summary>
    public class WeebCentralCatalogSyncService
    {
        private const int MaxParallelSeriesFetch = 3;
        private const string SourceName = "WeebCentral";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly object _statusLock = new();

        private bool _running;
        private int _processed;
        private int _total;
        private int _errors;
        private DateTime? _startedAt;
        private DateTime? _finishedAt;

        public WeebCentralCatalogSyncService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public class SyncStatus
        {
            public bool Running { get; set; }
            public int Processed { get; set; }
            public int Total { get; set; }
            public int Errors { get; set; }
            public DateTime? StartedAt { get; set; }
            public DateTime? FinishedAt { get; set; }
        }

        public SyncStatus GetStatus()
        {
            lock (_statusLock)
            {
                return new SyncStatus
                {
                    Running    = _running,
                    Processed  = _processed,
                    Total      = _total,
                    Errors     = _errors,
                    StartedAt  = _startedAt,
                    FinishedAt = _finishedAt
                };
            }
        }

        /// <summary>Kicks off a sweep in the background. Returns false if one is already running.</summary>
        public bool Start(int maxSeries, string? proxyUrl)
        {
            lock (_statusLock)
            {
                if (_running) return false;
                _running    = true;
                _processed  = 0;
                _total      = 0;
                _errors     = 0;
                _startedAt  = DateTime.Now;
                _finishedAt = null;
            }

            _ = Task.Run(() => RunSyncAsync(maxSeries, proxyUrl));
            return true;
        }

        private async Task RunSyncAsync(int maxSeries, string? proxyUrl)
        {
            var importer = new WeebCentralImporter(proxyUrl);

            try
            {
                var catalog = await importer.EnumerateCatalog(maxSeries);

                List<string> existingFonteIds;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    existingFonteIds = await db.Mangas
                        .Where(m => m.Fonte == SourceName && m.FonteId != null)
                        .Select(m => m.FonteId!)
                        .ToListAsync();
                }

                var existing = new HashSet<string>(existingFonteIds);
                var newEntries = catalog.Where(e => !existing.Contains(e.SeriesId)).ToList();

                lock (_statusLock) { _total = newEntries.Count; }

                using var httpThrottle = new SemaphoreSlim(MaxParallelSeriesFetch, MaxParallelSeriesFetch);
                using var dbWriteLock = new SemaphoreSlim(1, 1);

                var tasks = newEntries.Select(async entry =>
                {
                    await httpThrottle.WaitAsync();
                    try
                    {
                        // Request pacing/backoff for rate limits is handled centrally inside
                        // WeebCentralImporter (shared across all concurrent fetches).
                        var result = await importer.ImportSeriesShallow(entry.Url);

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
                            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                            db.Mangas.Add(manga);
                            await db.SaveChangesAsync();

                            foreach (var chapterData in chapters)
                            {
                                if (!decimal.TryParse(chapterData.ChapterNumber, out var chapterNum))
                                    chapterNum = 0;

                                db.Capitulos.Add(new Capitulo
                                {
                                    MangaId         = manga.Id,
                                    NumeroCapitulo  = (int)Math.Floor(chapterNum),
                                    NomeCapitulo    = chapterData.ChapterTitle,
                                    FonteCapituloId = chapterData.FonteCapituloId,
                                    DataCriacao     = DateTime.Now
                                });
                            }

                            await db.SaveChangesAsync();
                        }
                        finally
                        {
                            dbWriteLock.Release();
                        }
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
                });

                await Task.WhenAll(tasks);
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
    }
}
