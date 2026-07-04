using AnyComic.Application.Common;
using AnyComic.Models;
using Microsoft.EntityFrameworkCore;

namespace AnyComic.Application.Mangas;

public interface IMangaProgressService
{
    /// <summary>Marca um capítulo como lido (idempotente — ignora se já existir).</summary>
    Task MarkChapterReadAsync(int userId, int mangaId, int capituloId);

    /// <summary>Desmarca um capítulo lido (marcação manual do usuário).</summary>
    Task UnmarkChapterReadAsync(int userId, int capituloId);

    /// <summary>Ids dos capítulos que o usuário já leu neste mangá.</summary>
    Task<HashSet<int>> GetReadChapterIdsAsync(int userId, int mangaId);
}

public class MangaProgressService : IMangaProgressService
{
    private readonly IApplicationDbContext _db;

    public MangaProgressService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task MarkChapterReadAsync(int userId, int mangaId, int capituloId)
    {
        var existente = await _db.CapitulosLidos
            .FirstOrDefaultAsync(cl => cl.UsuarioId == userId && cl.CapituloId == capituloId);

        if (existente == null)
        {
            _db.CapitulosLidos.Add(new CapituloLido
            {
                UsuarioId   = userId,
                MangaId     = mangaId,
                CapituloId  = capituloId,
                DataLeitura = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }
    }

    public async Task UnmarkChapterReadAsync(int userId, int capituloId)
    {
        var registro = await _db.CapitulosLidos
            .FirstOrDefaultAsync(cl => cl.UsuarioId == userId && cl.CapituloId == capituloId);

        if (registro != null)
        {
            _db.CapitulosLidos.Remove(registro);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<HashSet<int>> GetReadChapterIdsAsync(int userId, int mangaId)
    {
        var ids = await _db.CapitulosLidos
            .Where(cl => cl.UsuarioId == userId && cl.MangaId == mangaId)
            .Select(cl => cl.CapituloId)
            .ToListAsync();

        return ids.ToHashSet();
    }
}
