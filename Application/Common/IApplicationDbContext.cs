using AnyComic.Models;
using Microsoft.EntityFrameworkCore;

namespace AnyComic.Application.Common;

/// <summary>
/// Persistence port for the application layer. Exposes only the sets the Manga + WeebCentral
/// slice needs, so services depend on this abstraction instead of the concrete DbContext.
/// Implemented by <c>AnyComic.Data.ApplicationDbContext</c>.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Manga> Mangas { get; }
    DbSet<Capitulo> Capitulos { get; }
    DbSet<PaginaManga> PaginasMangas { get; }
    DbSet<Favorito> Favoritos { get; }
    DbSet<ReviewManga> ReviewsManga { get; }
    DbSet<ReviewReplyManga> ReviewRepliesManga { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
