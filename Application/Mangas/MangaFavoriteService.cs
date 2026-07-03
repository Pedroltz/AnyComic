using AnyComic.Application.Common;
using AnyComic.Models;
using Microsoft.EntityFrameworkCore;

namespace AnyComic.Application.Mangas;

public interface IMangaFavoriteService
{
    Task<List<Favorito>> GetFavoritesAsync(int userId);
    Task AddAsync(int userId, int mangaId);
    Task RemoveAsync(int userId, int mangaId);
}

public class MangaFavoriteService : IMangaFavoriteService
{
    private readonly IApplicationDbContext _db;

    public MangaFavoriteService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<Favorito>> GetFavoritesAsync(int userId)
    {
        return await _db.Favoritos
            .Include(f => f.Manga)
            .Where(f => f.UsuarioId == userId)
            .OrderByDescending(f => f.DataAdicao)
            .ToListAsync();
    }

    public async Task AddAsync(int userId, int mangaId)
    {
        var existente = await _db.Favoritos
            .FirstOrDefaultAsync(f => f.UsuarioId == userId && f.MangaId == mangaId);

        if (existente == null)
        {
            _db.Favoritos.Add(new Favorito
            {
                UsuarioId  = userId,
                MangaId    = mangaId,
                DataAdicao = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }
    }

    public async Task RemoveAsync(int userId, int mangaId)
    {
        var favorito = await _db.Favoritos
            .FirstOrDefaultAsync(f => f.UsuarioId == userId && f.MangaId == mangaId);

        if (favorito != null)
        {
            _db.Favoritos.Remove(favorito);
            await _db.SaveChangesAsync();
        }
    }
}
