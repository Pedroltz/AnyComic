using AnyComic.Application.Common;
using AnyComic.Models;
using AnyComic.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AnyComic.Application.Mangas;

public interface IMangaReviewService
{
    /// <summary>Loads the manga's reviews (newest first, with replies) as a presentation section.</summary>
    Task<ReviewsSectionViewModel> GetSectionAsync(int mangaId, int? currentUserId, bool isAuthenticated, bool canReview);

    Task AddOrUpdateReviewAsync(int userId, int mangaId, int nota, string texto);
    Task DeleteReviewAsync(int userId, int mangaId);
    Task AddReplyAsync(int userId, int mangaId, int reviewId, string texto);
    Task DeleteReplyAsync(int userId, int replyId);
}

public class MangaReviewService : IMangaReviewService
{
    private const int MaxTextLength = 2000;

    private readonly IApplicationDbContext _db;

    public MangaReviewService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ReviewsSectionViewModel> GetSectionAsync(
        int mangaId, int? currentUserId, bool isAuthenticated, bool canReview)
    {
        var reviews = await _db.ReviewsManga
            .Include(r => r.Usuario)
            .Include(r => r.Replies).ThenInclude(rep => rep.Usuario)
            .Where(r => r.MangaId == mangaId)
            .OrderByDescending(r => r.DataAtualizacao ?? r.DataCriacao)
            .ToListAsync();

        return BuildReviewsSection(reviews, currentUserId, mangaId, isAuthenticated, canReview);
    }

    public async Task AddOrUpdateReviewAsync(int userId, int mangaId, int nota, string texto)
    {
        if (nota < 1 || nota > 5 || string.IsNullOrWhiteSpace(texto))
            return;

        texto = texto.Trim();
        if (texto.Length > MaxTextLength) texto = texto[..MaxTextLength];

        var review = await _db.ReviewsManga
            .FirstOrDefaultAsync(r => r.UsuarioId == userId && r.MangaId == mangaId);

        if (review == null)
        {
            _db.ReviewsManga.Add(new ReviewManga
            {
                UsuarioId   = userId,
                MangaId     = mangaId,
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

        await _db.SaveChangesAsync();
    }

    public async Task DeleteReviewAsync(int userId, int mangaId)
    {
        var review = await _db.ReviewsManga
            .FirstOrDefaultAsync(r => r.UsuarioId == userId && r.MangaId == mangaId);

        if (review != null)
        {
            _db.ReviewsManga.Remove(review);
            await _db.SaveChangesAsync();
        }
    }

    public async Task AddReplyAsync(int userId, int mangaId, int reviewId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return;

        texto = texto.Trim();
        if (texto.Length > MaxTextLength) texto = texto[..MaxTextLength];

        // Ensure the review exists and belongs to this manga before replying.
        var reviewExists = await _db.ReviewsManga
            .AnyAsync(r => r.Id == reviewId && r.MangaId == mangaId);

        if (reviewExists)
        {
            _db.ReviewRepliesManga.Add(new ReviewReplyManga
            {
                ReviewId    = reviewId,
                UsuarioId   = userId,
                Texto       = texto,
                DataCriacao = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }
    }

    public async Task DeleteReplyAsync(int userId, int replyId)
    {
        var reply = await _db.ReviewRepliesManga
            .FirstOrDefaultAsync(r => r.Id == replyId && r.UsuarioId == userId);

        if (reply != null)
        {
            _db.ReviewRepliesManga.Remove(reply);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>Projects loaded manga reviews into the shared presentation view model.</summary>
    private static ReviewsSectionViewModel BuildReviewsSection(
        List<ReviewManga> reviews, int? currentUserId, int mangaId, bool isAuthenticated, bool canReview)
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
            Controller      = "Manga",
            WorkId          = mangaId,
            Reviews         = items,
            UserReview      = currentUserId.HasValue ? items.FirstOrDefault(i => i.UsuarioId == currentUserId.Value) : null,
            IsAuthenticated = isAuthenticated,
            CanReview       = canReview,
            CurrentUserId   = currentUserId
        };
    }
}
