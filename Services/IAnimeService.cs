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
    Task                               AddReviewAsync(int animeId, int userId, int nota, string texto);
    Task                               DeleteReviewAsync(int animeId, int userId);
    Task                               AddReplyAsync(int animeId, int reviewId, int userId, string texto);
    Task                               DeleteReplyAsync(int replyId, int userId);
}
