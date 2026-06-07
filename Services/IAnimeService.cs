using AnyComic.Models;
using AnyComic.Models.ViewModels;

namespace AnyComic.Services;

public interface IAnimeService
{
    Task<IReadOnlyList<Anime>>         GetListAsync(AnimeSearchFilter filter);
    Task<AnimeDetailsViewModel?>       GetDetailsAsync(int animeId, int? episodeNumber, int? userId);
    Task<IReadOnlyList<FavoritoAnime>> GetFavoritosAsync(int userId);
    Task                               AddFavoritoAsync(int animeId, int userId);
    Task                               RemoveFavoritoAsync(int animeId, int userId);
}
