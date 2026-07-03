using AnyComic.Application.Common;
using AnyComic.Domain.Interfaces;
using AnyComic.Models;

namespace AnyComic.Application.WeebCentral;

/// <summary>Outcome of a single-URL WeebCentral import (shape mirrors the admin AJAX response).</summary>
public class WeebCentralImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int MangaId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public int TotalChapters { get; set; }
    public int TotalPages { get; set; }
    public List<string> Chapters { get; set; } = new();
}

public interface IWeebCentralImportService
{
    Task<WeebCentralImportResult> ImportAsync(string url, string chapterRange, string? proxyUrl);
}

/// <summary>
/// Imports a single manga (by URL) from WeebCentral: fetches metadata + chapter pages via the
/// scraper and persists Manga/Capitulo/PaginaManga.
/// </summary>
public class WeebCentralImportService : IWeebCentralImportService
{
    private readonly IApplicationDbContext _db;
    private readonly IWeebCentralScraperFactory _scraperFactory;

    public WeebCentralImportService(IApplicationDbContext db, IWeebCentralScraperFactory scraperFactory)
    {
        _db = db;
        _scraperFactory = scraperFactory;
    }

    public async Task<WeebCentralImportResult> ImportAsync(string url, string chapterRange, string? proxyUrl)
    {
        var scraper = _scraperFactory.Create(proxyUrl);
        var result = await scraper.ImportFromUrl(url, string.IsNullOrEmpty(chapterRange) ? "all" : chapterRange);

        if (!result.HasValue)
        {
            return new WeebCentralImportResult
            {
                Success = false,
                Message = "Failed to import from WeebCentral. Please check the URL and try again."
            };
        }

        var importedManga = result.Value.manga;
        var chapters = result.Value.chapters;

        if (chapters.Count == 0)
        {
            return new WeebCentralImportResult
            {
                Success = false,
                Message = "No chapters were successfully indexed"
            };
        }

        _db.Mangas.Add(importedManga);
        await _db.SaveChangesAsync();

        int totalPages = 0;
        var chapterSummaries = new List<string>();

        foreach (var chapterData in chapters)
        {
            if (!decimal.TryParse(chapterData.ChapterNumber, out decimal chapterNum))
                chapterNum = 0;

            var capitulo = new Capitulo
            {
                MangaId         = importedManga.Id,
                NumeroCapitulo  = (int)Math.Floor(chapterNum),
                NomeCapitulo    = chapterData.ChapterTitle,
                FonteCapituloId = chapterData.FonteCapituloId,
                DataCriacao     = DateTime.Now
            };
            _db.Capitulos.Add(capitulo);
            await _db.SaveChangesAsync();

            int pageNumber = 1;
            foreach (var pageUrl in chapterData.PageUrls)
            {
                _db.PaginasMangas.Add(new PaginaManga
                {
                    MangaId       = importedManga.Id,
                    CapituloId    = capitulo.Id,
                    NumeroPagina  = pageNumber++,
                    CaminhoImagem = pageUrl,
                    DataUpload    = DateTime.Now
                });
            }

            await _db.SaveChangesAsync();

            totalPages += chapterData.PageUrls.Count;
            chapterSummaries.Add($"Chapter {chapterData.ChapterNumber}: {chapterData.PageUrls.Count} pages");
        }

        return new WeebCentralImportResult
        {
            Success       = true,
            MangaId       = importedManga.Id,
            Titulo        = importedManga.Titulo,
            Autor         = importedManga.Autor,
            Descricao     = importedManga.Descricao,
            TotalChapters = chapters.Count,
            TotalPages    = totalPages,
            Chapters      = chapterSummaries,
            Message       = $"Manga '{importedManga.Titulo}' imported successfully with {chapters.Count} chapter(s) and {totalPages} total pages!"
        };
    }
}
