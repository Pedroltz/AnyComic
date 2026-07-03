using AnyComic.Application.Common;
using AnyComic.Domain.Interfaces;
using AnyComic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AnyComic.Application.Manga;

public interface IMangaReaderService
{
    /// <summary>Details page data, or null if the manga doesn't exist.</summary>
    Task<MangaDetailsResult?> GetDetailsAsync(int id, int? currentUserId, bool isAuthenticated, bool canReview);

    /// <summary>Reader page data, or a not-found/redirect signal.</summary>
    Task<MangaReaderResult> GetChapterAsync(int id, int? capituloNumero, string pagina);
}

public class MangaReaderService : IMangaReaderService
{
    private readonly IApplicationDbContext _db;
    private readonly IMangaReviewService _reviewService;
    private readonly IWeebCentralScraperFactory _scraperFactory;
    private readonly IConfiguration _configuration;

    public MangaReaderService(
        IApplicationDbContext db,
        IMangaReviewService reviewService,
        IWeebCentralScraperFactory scraperFactory,
        IConfiguration configuration)
    {
        _db = db;
        _reviewService = reviewService;
        _scraperFactory = scraperFactory;
        _configuration = configuration;
    }

    public async Task<MangaDetailsResult?> GetDetailsAsync(int id, int? currentUserId, bool isAuthenticated, bool canReview)
    {
        var manga = await _db.Mangas
            .Include(m => m.Capitulos.OrderBy(c => c.NumeroCapitulo))
            .FirstOrDefaultAsync(m => m.Id == id);

        if (manga == null)
            return null;

        // Load only page counts per chapter (not full page entities)
        var pageCountsByChapter = await _db.PaginasMangas
            .Where(p => p.MangaId == id)
            .GroupBy(p => p.CapituloId)
            .Select(g => new { CapituloId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CapituloId, x => x.Count);

        var totalPages = pageCountsByChapter.Values.Sum();

        // "Start Reading" is available when the manga is readable: either pages are already
        // indexed, or at least one chapter can be lazily page-indexed on open.
        bool hasLazyIndexableChapter = manga.Capitulos.Any(c => !string.IsNullOrEmpty(c.FonteCapituloId));
        bool hasPages = totalPages > 0 || hasLazyIndexableChapter;

        bool isFavorito = false;
        if (canReview && currentUserId.HasValue)
        {
            isFavorito = await _db.Favoritos
                .AnyAsync(f => f.UsuarioId == currentUserId.Value && f.MangaId == id);
        }

        var reviews = await _reviewService.GetSectionAsync(id, currentUserId, isAuthenticated, canReview);

        return new MangaDetailsResult
        {
            Manga               = manga,
            PageCountsByChapter = pageCountsByChapter,
            TotalPages          = totalPages,
            HasPages            = hasPages,
            IsFavorito          = isFavorito,
            Reviews             = reviews
        };
    }

    public async Task<MangaReaderResult> GetChapterAsync(int id, int? capituloNumero, string pagina)
    {
        var manga = await _db.Mangas
            .Include(m => m.Capitulos.OrderBy(c => c.NumeroCapitulo))
                .ThenInclude(c => c.Paginas.OrderBy(p => p.NumeroPagina))
            .FirstOrDefaultAsync(m => m.Id == id);

        if (manga == null)
            return MangaReaderResult.NotFoundResult();

        if (!manga.Capitulos.Any())
            return MangaReaderResult.Redirect(id);

        // If no chapter specified, start from the first chapter
        Capitulo? capituloAtual;
        if (capituloNumero == null)
        {
            capituloAtual = manga.Capitulos.OrderBy(c => c.NumeroCapitulo).First();
        }
        else
        {
            capituloAtual = manga.Capitulos.FirstOrDefault(c => c.NumeroCapitulo == capituloNumero)
                            ?? manga.Capitulos.OrderBy(c => c.NumeroCapitulo).First();
        }

        // Chapters imported by the WeebCentral catalog sweep have no pages yet —
        // index them lazily the first time a reader opens the chapter.
        if (!string.IsNullOrEmpty(capituloAtual.FonteCapituloId) && !capituloAtual.Paginas.Any())
        {
            var proxyUrl = _configuration["WeebCentral:ProxyUrl"];
            var scraper = _scraperFactory.Create(proxyUrl);
            var chapterDto = new WeebCentralChapter
            {
                Id            = capituloAtual.FonteCapituloId,
                ChapterNumber = capituloAtual.NumeroCapitulo,
                ChapterTitle  = capituloAtual.NomeCapitulo ?? ""
            };

            var pageUrls = await scraper.IndexChapterPages(chapterDto, string.Empty);
            if (pageUrls.Count > 0)
            {
                int pageNumber = 1;
                var novasPaginas = pageUrls.Select(url => new PaginaManga
                {
                    MangaId       = manga.Id,
                    CapituloId    = capituloAtual.Id,
                    NumeroPagina  = pageNumber++,
                    CaminhoImagem = url,
                    DataUpload    = DateTime.Now
                }).ToList();

                _db.PaginasMangas.AddRange(novasPaginas);
                await _db.SaveChangesAsync();

                capituloAtual.Paginas = novasPaginas;
            }
        }

        if (!capituloAtual.Paginas.Any())
            return MangaReaderResult.Redirect(id);

        // Get the requested page from the current chapter
        PaginaManga? paginaAtual;
        if (pagina.Equals("last", StringComparison.OrdinalIgnoreCase))
        {
            paginaAtual = capituloAtual.Paginas.OrderByDescending(p => p.NumeroPagina).First();
        }
        else
        {
            int.TryParse(pagina, out int paginaNum);
            paginaAtual = capituloAtual.Paginas.FirstOrDefault(p => p.NumeroPagina == paginaNum)
                          ?? capituloAtual.Paginas.OrderBy(p => p.NumeroPagina).First();
        }

        // Chapter-scoped navigation
        var paginasDoCapitulo = capituloAtual.Paginas.OrderBy(p => p.NumeroPagina).ToList();

        // Build page map only for the current chapter (includes image paths for client-side navigation)
        var pageMap = paginasDoCapitulo
            .Select((p, index) => new ReaderPage(p.NumeroPagina, index + 1, EnsureProxied(p.CaminhoImagem)))
            .ToList();

        // Determine next/previous chapters
        var capitulosOrdenados = manga.Capitulos.OrderBy(c => c.NumeroCapitulo).ToList();
        var capituloIndex = capitulosOrdenados.FindIndex(c => c.Id == capituloAtual.Id);
        var proximoCapitulo = capituloIndex < capitulosOrdenados.Count - 1
            ? capitulosOrdenados[capituloIndex + 1] : null;
        var capituloAnterior = capituloIndex > 0
            ? capitulosOrdenados[capituloIndex - 1] : null;

        return new MangaReaderResult
        {
            Status           = MangaReaderStatus.Success,
            MangaId          = manga.Id,
            PaginaAtual      = paginaAtual,
            TotalPaginas     = paginasDoCapitulo.Count,
            PaginaNumero     = paginaAtual.NumeroPagina,
            PaginaImagemUrl  = EnsureProxied(paginaAtual.CaminhoImagem),
            MangaTitulo      = manga.Titulo,
            CapituloAtual    = capituloAtual,
            TotalCapitulos   = manga.Capitulos.Count,
            PageMap          = pageMap,
            Capitulos        = capitulosOrdenados,
            ProximoCapitulo  = proximoCapitulo,
            CapituloAnterior = capituloAnterior
        };
    }

    /// <summary>
    /// Wraps external CDN image URLs through the local proxy so hotlink protection is bypassed.
    /// Leaves local paths and already-proxied URLs untouched.
    /// </summary>
    private static string EnsureProxied(string url)
    {
        if (string.IsNullOrEmpty(url) || url.StartsWith("/Proxy/") || url.StartsWith("/uploads/") || url.StartsWith("/images/"))
            return url;

        if (url.StartsWith("https://uploads.mangadex.org") || url.StartsWith("http://uploads.mangadex.org"))
            return $"/Proxy/Image?url={Uri.EscapeDataString(url)}";

        return url;
    }
}
