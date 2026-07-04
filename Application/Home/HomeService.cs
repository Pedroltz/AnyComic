using System.Linq.Expressions;
using AnyComic.Application.Common;
using AnyComic.Models;
using AnyComic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AnyComic.Application.Home;

public interface IHomeService
{
    Task<HomeViewModel> GetHomeAsync();
}

/// <summary>
/// Assembles the home page: the active banner carousel plus several curated manga rows
/// (newest, recently updated, most favorited, top rated, discover). Each row is capped, so
/// the catalog is never loaded in full. Engagement-based rows are omitted when they have no data.
/// </summary>
public class HomeService : IHomeService
{
    private const int RowSize = 12;
    private const int GridSize = 24; // ~4 rows of cards for the Discover grid

    private readonly IApplicationDbContext _db;

    public HomeService(IApplicationDbContext db)
    {
        _db = db;
    }

    // Lightweight card projection reused by every row (chapter count via subquery).
    private static readonly Expression<Func<Manga, MangaCardViewModel>> ToCard = m => new MangaCardViewModel
    {
        Id           = m.Id,
        Titulo       = m.Titulo,
        Autor        = m.Autor,
        ImagemCapa   = m.ImagemCapa,
        DataCriacao  = m.DataCriacao,
        ChapterCount = m.Capitulos.Count
    };

    public async Task<HomeViewModel> GetHomeAsync()
    {
        var banners = await _db.Banners
            .Include(b => b.Manga)
            .Where(b => b.Ativo)
            .OrderBy(b => b.Ordem)
            .ToListAsync();

        var newest = await _db.Mangas
            .OrderByDescending(m => m.DataCriacao)
            .Take(RowSize)
            .Select(ToCard)
            .ToListAsync();

        var latestUpdates = await _db.Mangas
            .Where(m => m.Capitulos.Any())
            .OrderByDescending(m => m.Capitulos.Max(c => c.DataCriacao))
            .Take(RowSize)
            .Select(ToCard)
            .ToListAsync();

        var topRated = await _db.Mangas
            .Where(m => m.Reviews.Any())
            .OrderByDescending(m => m.Reviews.Average(r => r.Nota))
            .ThenByDescending(m => m.Reviews.Count())
            .Take(RowSize)
            .Select(ToCard)
            .ToListAsync();

        var discover = await GetDiscoverAsync();

        var sections = new List<HomeSectionViewModel>();

        AddSection(sections, "Newest", "Recently added manga and comics", "new", null, newest);
        AddSection(sections, "Latest Updates", "Series with freshly added chapters", "clock", null, latestUpdates);
        AddSection(sections, "Top Rated", "Highest rated by readers", "star", null, topRated);
        AddSection(sections, "Discover", "Explore something new", "shuffle", "Manga", discover, grid: true);

        return new HomeViewModel { Banners = banners, Sections = sections };
    }

    /// <summary>Cheap "random-ish" row: a random contiguous slice, varying each page load.</summary>
    private async Task<List<MangaCardViewModel>> GetDiscoverAsync()
    {
        var count = await _db.Mangas.CountAsync();
        if (count == 0) return new List<MangaCardViewModel>();

        var skip = count > GridSize ? Random.Shared.Next(0, count - GridSize) : 0;

        return await _db.Mangas
            .OrderBy(m => m.Id)
            .Skip(skip)
            .Take(GridSize)
            .Select(ToCard)
            .ToListAsync();
    }

    private static void AddSection(
        List<HomeSectionViewModel> sections,
        string title, string subtitle, string icon, string? viewAllController,
        List<MangaCardViewModel> items, bool grid = false)
    {
        if (items.Count == 0) return;

        sections.Add(new HomeSectionViewModel
        {
            Title = title,
            Subtitle = subtitle,
            Icon = icon,
            ViewAllController = viewAllController,
            Grid = grid,
            Items = items
        });
    }
}
