using Microsoft.EntityFrameworkCore;
using AnyComic.Data;
using AnyComic.Models;
using AnyComic.Models.ViewModels;

namespace AnyComic.Services;

public class AnimeService(ApplicationDbContext context) : IAnimeService
{
    public async Task<IReadOnlyList<Anime>> GetListAsync(AnimeSearchFilter filter)
    {
        var query = context.Animes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            query = query.Where(a => a.Titulo.Contains(filter.SearchTerm));

        if (!string.IsNullOrWhiteSpace(filter.Autor))
            query = query.Where(a => a.Autor.Contains(filter.Autor));

        if (filter.DataInicio.HasValue)
            query = query.Where(a => a.DataCriacao >= filter.DataInicio.Value);

        if (filter.DataFim.HasValue)
            query = query.Where(a => a.DataCriacao <= filter.DataFim.Value);

        query = filter.SortBy switch
        {
            "titulo_asc"  => query.OrderBy(a => a.Titulo),
            "titulo_desc" => query.OrderByDescending(a => a.Titulo),
            "autor_asc"   => query.OrderBy(a => a.Autor),
            "autor_desc"  => query.OrderByDescending(a => a.Autor),
            "data_asc"    => query.OrderBy(a => a.DataCriacao),
            "data_desc"   => query.OrderByDescending(a => a.DataCriacao),
            _             => query.OrderByDescending(a => a.DataCriacao)
        };

        return await query.Include(a => a.Episodios).ToListAsync();
    }

    public async Task<AnimeDetailsViewModel?> GetDetailsAsync(int animeId, int? episodeNumber, int? userId)
    {
        var anime = await context.Animes
            .Include(a => a.Episodios.OrderBy(e => e.NumeroEpisodio))
            .FirstOrDefaultAsync(a => a.Id == animeId);

        if (anime == null) return null;

        var episodios = anime.Episodios.OrderBy(e => e.NumeroEpisodio).ToList();

        var episodioAtual = episodeNumber.HasValue
            ? episodios.FirstOrDefault(e => e.NumeroEpisodio == episodeNumber.Value)
            : null;

        Episodio? proximo  = null;
        Episodio? anterior = null;
        if (episodioAtual != null)
        {
            var idx  = episodios.FindIndex(e => e.Id == episodioAtual.Id);
            proximo  = idx < episodios.Count - 1 ? episodios[idx + 1] : null;
            anterior = idx > 0                   ? episodios[idx - 1] : null;
        }

        var isFavorito = userId.HasValue
            && await context.FavoritosAnime
                .AnyAsync(f => f.UsuarioId == userId.Value && f.AnimeId == animeId);

        var reviews = await context.ReviewsAnime
            .Include(r => r.Usuario)
            .Include(r => r.Replies).ThenInclude(rep => rep.Usuario)
            .Where(r => r.AnimeId == animeId)
            .OrderByDescending(r => r.DataAtualizacao ?? r.DataCriacao)
            .ToListAsync();

        return new AnimeDetailsViewModel
        {
            Anime            = anime,
            Episodios        = episodios,
            EpisodioAtual    = episodioAtual,
            ProximoEpisodio  = proximo,
            EpisodioAnterior = anterior,
            IsFavorito       = isFavorito,
            Player           = ResolvePlayer(episodioAtual),
            Reviews          = BuildReviewsSection(reviews, userId, animeId)
        };
    }

    private static ReviewsSectionViewModel BuildReviewsSection(List<ReviewAnime> reviews, int? currentUserId, int animeId)
    {
        var items = reviews.Select(r => new ReviewItemViewModel
        {
            ReviewId    = r.Id,
            UsuarioId   = r.UsuarioId,
            UsuarioNome = r.Usuario?.Nome ?? "User",
            UsuarioFoto = r.Usuario?.FotoPerfil,
            Nota        = r.Nota,
            Texto       = r.Texto,
            Data        = r.DataAtualizacao ?? r.DataCriacao,
            Editado     = r.DataAtualizacao != null,
            Replies     = r.Replies
                .OrderBy(rep => rep.DataCriacao)
                .Select(rep => new ReviewReplyItemViewModel
                {
                    Id          = rep.Id,
                    UsuarioId   = rep.UsuarioId,
                    UsuarioNome = rep.Usuario?.Nome ?? "User",
                    UsuarioFoto = rep.Usuario?.FotoPerfil,
                    Texto       = rep.Texto,
                    Data        = rep.DataAtualizacao ?? rep.DataCriacao,
                    Editado     = rep.DataAtualizacao != null
                }).ToList()
        }).ToList();

        return new ReviewsSectionViewModel
        {
            Controller      = "Anime",
            WorkId          = animeId,
            Reviews         = items,
            UserReview      = currentUserId.HasValue ? items.FirstOrDefault(i => i.UsuarioId == currentUserId.Value) : null,
            IsAuthenticated = currentUserId.HasValue,
            CanReview       = currentUserId.HasValue,
            CurrentUserId   = currentUserId
        };
    }

    public async Task AddReviewAsync(int animeId, int userId, int nota, string texto)
    {
        if (nota < 1 || nota > 5 || string.IsNullOrWhiteSpace(texto))
            return;

        texto = texto.Trim();
        if (texto.Length > 2000) texto = texto[..2000];

        var review = await context.ReviewsAnime
            .FirstOrDefaultAsync(r => r.UsuarioId == userId && r.AnimeId == animeId);

        if (review == null)
        {
            context.ReviewsAnime.Add(new ReviewAnime
            {
                UsuarioId   = userId,
                AnimeId     = animeId,
                Nota        = nota,
                Texto       = texto,
                DataCriacao = DateTime.Now
            });
        }
        else
        {
            review.Nota            = nota;
            review.Texto           = texto;
            review.DataAtualizacao = DateTime.Now;
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteReviewAsync(int animeId, int userId)
    {
        var review = await context.ReviewsAnime
            .FirstOrDefaultAsync(r => r.UsuarioId == userId && r.AnimeId == animeId);

        if (review != null)
        {
            context.ReviewsAnime.Remove(review);
            await context.SaveChangesAsync();
        }
    }

    public async Task AddReplyAsync(int animeId, int reviewId, int userId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return;

        texto = texto.Trim();
        if (texto.Length > 2000) texto = texto[..2000];

        var reviewExists = await context.ReviewsAnime
            .AnyAsync(r => r.Id == reviewId && r.AnimeId == animeId);

        if (reviewExists)
        {
            context.ReviewRepliesAnime.Add(new ReviewReplyAnime
            {
                ReviewId    = reviewId,
                UsuarioId   = userId,
                Texto       = texto,
                DataCriacao = DateTime.Now
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteReplyAsync(int replyId, int userId)
    {
        var reply = await context.ReviewRepliesAnime
            .FirstOrDefaultAsync(r => r.Id == replyId && r.UsuarioId == userId);

        if (reply != null)
        {
            context.ReviewRepliesAnime.Remove(reply);
            await context.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<FavoritoAnime>> GetFavoritosAsync(int userId)
        => await context.FavoritosAnime
            .Include(f => f.Anime)
            .Where(f => f.UsuarioId == userId)
            .OrderByDescending(f => f.DataAdicao)
            .ToListAsync();

    public async Task AddFavoritoAsync(int animeId, int userId)
    {
        var exists = await context.FavoritosAnime
            .AnyAsync(f => f.UsuarioId == userId && f.AnimeId == animeId);

        if (!exists)
        {
            context.FavoritosAnime.Add(new FavoritoAnime
            {
                UsuarioId  = userId,
                AnimeId    = animeId,
                DataAdicao = DateTime.Now
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task RemoveFavoritoAsync(int animeId, int userId)
    {
        var favorito = await context.FavoritosAnime
            .FirstOrDefaultAsync(f => f.UsuarioId == userId && f.AnimeId == animeId);

        if (favorito != null)
        {
            context.FavoritosAnime.Remove(favorito);
            await context.SaveChangesAsync();
        }
    }

    // ── Player resolution ──────────────────────────────────────────────────────

    private static readonly string[] VideoExtensions = [".mp4", ".webm", ".ogg", ".mov", ".m4v"];

    private static PlayerInfo ResolvePlayer(Episodio? episodio)
    {
        var link = episodio?.LinkVideo?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(link)) return new PlayerInfo();

        var isHttp = link.StartsWith("http://",  StringComparison.OrdinalIgnoreCase)
                  || link.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var isFile = VideoExtensions.Any(e => link.EndsWith(e, StringComparison.OrdinalIgnoreCase))
                  || link.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase);

        if (!isHttp || isFile)
            return new PlayerInfo { Kind = PlayerKind.File, EmbedUrl = link };

        var youtubeId = ExtractYouTubeId(link);
        if (youtubeId != null)
            return new PlayerInfo { Kind = PlayerKind.YouTube, EmbedUrl = link, YoutubeId = youtubeId };

        var vimeoId = ExtractVimeoId(link);
        if (vimeoId != null)
            return new PlayerInfo { Kind = PlayerKind.Vimeo, EmbedUrl = link, VimeoId = vimeoId };

        return new PlayerInfo { Kind = PlayerKind.Iframe, EmbedUrl = link };
    }

    private static string? ExtractYouTubeId(string url)
    {
        if (url.Contains("youtube.com/watch",  StringComparison.OrdinalIgnoreCase)) return Segment(url, "v=");
        if (url.Contains("youtu.be/",          StringComparison.OrdinalIgnoreCase)) return Segment(url, "youtu.be/");
        if (url.Contains("youtube.com/embed/", StringComparison.OrdinalIgnoreCase)) return Segment(url, "embed/");
        return null;
    }

    private static string? ExtractVimeoId(string url)
        => url.Contains("vimeo.com/", StringComparison.OrdinalIgnoreCase)
            ? Segment(url, "vimeo.com/")
            : null;

    private static string? Segment(string src, string token)
    {
        var i = src.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var rest = src[(i + token.Length)..];
        var stop = rest.IndexOfAny(['?', '&', '/', '#']);
        var result = stop >= 0 ? rest[..stop] : rest;
        return string.IsNullOrEmpty(result) ? null : result;
    }
}
