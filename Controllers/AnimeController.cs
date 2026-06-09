using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AnyComic.Models.ViewModels;
using AnyComic.Services;

namespace AnyComic.Controllers;

public class AnimeController(IAnimeService animeService) : Controller
{
    public async Task<IActionResult> Index(AnimeSearchFilter filter)
    {
        var animes = await animeService.GetListAsync(filter);

        // ViewBag preservado para popular o formulário de filtros na view
        ViewBag.SearchTerm = filter.SearchTerm;
        ViewBag.Autor      = filter.Autor;
        ViewBag.DataInicio = filter.DataInicio?.ToString("yyyy-MM-dd");
        ViewBag.DataFim    = filter.DataFim?.ToString("yyyy-MM-dd");
        ViewBag.SortBy     = filter.SortBy;

        return View(animes);
    }

    public async Task<IActionResult> Details(int? id, int? ep = null)
    {
        if (id == null) return NotFound();

        var vm = await animeService.GetDetailsAsync(id.Value, ep, GetUserId());
        return vm == null ? NotFound() : View(vm);
    }

    // Mantido para compatibilidade com links externos; redireciona para Details
    public IActionResult Watch(int? id, int? episodioNumero = null)
        => RedirectToAction(nameof(Details), new { id, ep = episodioNumero });

    [Authorize]
    public async Task<IActionResult> Favoritos()
    {
        var favoritos = await animeService.GetFavoritosAsync(GetUserId()!.Value);
        return View(favoritos);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFavorito(int id)
    {
        await animeService.AddFavoritoAsync(id, GetUserId()!.Value);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFavorito(int id, string? returnUrl)
    {
        await animeService.RemoveFavoritoAsync(id, GetUserId()!.Value);
        return returnUrl == "favoritos"
            ? RedirectToAction(nameof(Favoritos))
            : RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(int animeId, int nota, string texto)
    {
        await animeService.AddReviewAsync(animeId, GetUserId()!.Value, nota, texto);
        return RedirectToAction(nameof(Details), new { id = animeId });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int animeId)
    {
        await animeService.DeleteReviewAsync(animeId, GetUserId()!.Value);
        return RedirectToAction(nameof(Details), new { id = animeId });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReply(int animeId, int reviewId, string texto)
    {
        await animeService.AddReplyAsync(animeId, reviewId, GetUserId()!.Value, texto);
        return RedirectToAction(nameof(Details), new { id = animeId });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReply(int animeId, int replyId)
    {
        await animeService.DeleteReplyAsync(replyId, GetUserId()!.Value);
        return RedirectToAction(nameof(Details), new { id = animeId });
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim.Value) : null;
    }
}
