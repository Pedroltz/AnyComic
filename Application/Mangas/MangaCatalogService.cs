using AnyComic.Application.Common;
using AnyComic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AnyComic.Application.Mangas;

public interface IMangaCatalogService
{
    Task<MangaCatalogViewModel> GetCatalogAsync(
        string? searchTerm, string? autor, DateTime? dataInicio, DateTime? dataFim,
        string? sortBy, int page);

    /// <summary>Lightweight title matches for the header autocomplete (max <paramref name="limit"/>).</summary>
    Task<List<MangaSuggestion>> SearchSuggestionsAsync(string term, int limit);
}

/// <summary>Minimal manga projection for the header search dropdown.</summary>
public record MangaSuggestion(int Id, string Titulo, string Autor, string? ImagemCapa);

/// <summary>
/// Builds the paged manga catalog listing. Counts first, then fetches only the requested page,
/// projecting the chapter count via a correlated subquery (chapters are never materialized).
/// </summary>
public class MangaCatalogService : IMangaCatalogService
{
    private const int CatalogPageSize = 100;

    private readonly IApplicationDbContext _db;

    public MangaCatalogService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<MangaCatalogViewModel> GetCatalogAsync(
        string? searchTerm, string? autor, DateTime? dataInicio, DateTime? dataFim,
        string? sortBy, int page)
    {
        var query = _db.Mangas.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(m => m.Titulo.Contains(searchTerm));

        if (!string.IsNullOrWhiteSpace(autor))
            query = query.Where(m => m.Autor.Contains(autor));

        if (dataInicio.HasValue)
            query = query.Where(m => m.DataCriacao >= dataInicio.Value);

        if (dataFim.HasValue)
            query = query.Where(m => m.DataCriacao <= dataFim.Value);

        query = sortBy switch
        {
            "titulo_asc"  => query.OrderBy(m => m.Titulo),
            "titulo_desc" => query.OrderByDescending(m => m.Titulo),
            "autor_asc"   => query.OrderBy(m => m.Autor),
            "autor_desc"  => query.OrderByDescending(m => m.Autor),
            "data_asc"    => query.OrderBy(m => m.DataCriacao),
            "data_desc"   => query.OrderByDescending(m => m.DataCriacao),
            _             => query.OrderByDescending(m => m.DataCriacao) // Padrão: mais recentes primeiro
        };

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)CatalogPageSize);

        if (page < 1) page = 1;
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var mangas = await query
            .Skip((page - 1) * CatalogPageSize)
            .Take(CatalogPageSize)
            .Select(m => new MangaCardViewModel
            {
                Id           = m.Id,
                Titulo       = m.Titulo,
                Autor        = m.Autor,
                ImagemCapa   = m.ImagemCapa,
                DataCriacao  = m.DataCriacao,
                ChapterCount = m.Capitulos.Count
            })
            .ToListAsync();

        return new MangaCatalogViewModel
        {
            Mangas     = mangas,
            Page       = page < 1 ? 1 : page,
            PageSize   = CatalogPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            SearchTerm = searchTerm,
            Autor      = autor,
            DataInicio = dataInicio,
            DataFim    = dataFim,
            SortBy     = sortBy
        };
    }

    public async Task<List<MangaSuggestion>> SearchSuggestionsAsync(string term, int limit)
    {
        if (string.IsNullOrWhiteSpace(term))
            return new List<MangaSuggestion>();

        term = term.Trim();

        return await _db.Mangas
            .Where(m => m.Titulo.Contains(term))
            .OrderBy(m => m.Titulo)
            .Take(limit)
            .Select(m => new MangaSuggestion(m.Id, m.Titulo, m.Autor, m.ImagemCapa))
            .ToListAsync();
    }
}
