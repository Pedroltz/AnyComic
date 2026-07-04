using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AnyComic.Application.Mangas;

namespace AnyComic.Controllers
{
    /// <summary>
    /// Thin presentation layer for the manga catalog, reader, favorites and reviews.
    /// All data access and business logic live in the Application services it depends on.
    /// </summary>
    public class MangaController : Controller
    {
        private readonly IMangaCatalogService _catalog;
        private readonly IMangaReaderService _reader;
        private readonly IMangaFavoriteService _favorites;
        private readonly IMangaProgressService _progress;
        private readonly IMangaReviewService _reviews;

        public MangaController(
            IMangaCatalogService catalog,
            IMangaReaderService reader,
            IMangaFavoriteService favorites,
            IMangaProgressService progress,
            IMangaReviewService reviews)
        {
            _catalog = catalog;
            _reader = reader;
            _favorites = favorites;
            _progress = progress;
            _reviews = reviews;
        }

        private int CurrentUserId() =>
            int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);

        // GET: Manga/Index
        public async Task<IActionResult> Index(string? searchTerm, string? autor, DateTime? dataInicio, DateTime? dataFim, string? sortBy, int page = 1)
        {
            var viewModel = await _catalog.GetCatalogAsync(searchTerm, autor, dataInicio, dataFim, sortBy, page);

            // Filtros atuais também via ViewBag (a view de busca/ordenação já os consome)
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Autor = autor;
            ViewBag.DataInicio = dataInicio?.ToString("yyyy-MM-dd");
            ViewBag.DataFim = dataFim?.ToString("yyyy-MM-dd");
            ViewBag.SortBy = sortBy;

            return View(viewModel);
        }

        // GET: Manga/Suggest?term=... — live autocomplete for the header search
        [HttpGet]
        public async Task<IActionResult> Suggest(string? term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            {
                return Json(Array.Empty<object>());
            }

            var results = await _catalog.SearchSuggestionsAsync(term, 8);
            return Json(results);
        }

        // GET: Manga/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            bool isAuthenticated = User.Identity?.IsAuthenticated == true;
            bool isAdmin = User.FindFirstValue("IsAdmin") == "True";
            bool canReview = isAuthenticated && !isAdmin;
            int? currentUserId = canReview ? CurrentUserId() : null;

            var result = await _reader.GetDetailsAsync(id.Value, currentUserId, isAuthenticated, canReview);
            if (result == null)
            {
                return NotFound();
            }

            ViewBag.PageCountsByChapter = result.PageCountsByChapter;
            ViewBag.TotalPages = result.TotalPages;
            ViewBag.HasPages = result.HasPages;
            if (canReview) ViewBag.IsFavorito = result.IsFavorito;
            ViewBag.ReadChapterIds = result.ReadChapterIds;
            ViewBag.LastReadChapterNumber = result.LastReadChapterNumber;
            ViewBag.CanTrackProgress = canReview;
            ViewBag.Reviews = result.Reviews;

            return View(result.Manga);
        }

        // GET: Manga/Read/5
        public async Task<IActionResult> Read(int? id, int? capituloNumero = null, string pagina = "1")
        {
            if (id == null)
            {
                return NotFound();
            }

            var result = await _reader.GetChapterAsync(id.Value, capituloNumero, pagina);

            switch (result.Status)
            {
                case MangaReaderStatus.NotFound:
                    return NotFound();
                case MangaReaderStatus.RedirectToDetails:
                    return RedirectToAction(nameof(Details), new { id = result.MangaId });
            }

            ViewBag.TotalPaginas = result.TotalPaginas;
            ViewBag.PaginaAtual = result.PaginaNumero;
            ViewBag.PaginaImagemUrl = result.PaginaImagemUrl;
            ViewBag.MangaId = result.MangaId;
            ViewBag.MangaTitulo = result.MangaTitulo;
            ViewBag.CapituloAtual = result.CapituloAtual;
            ViewBag.TotalCapitulos = result.TotalCapitulos;
            ViewBag.PageMap = result.PageMap;
            ViewBag.Capitulos = result.Capitulos;
            ViewBag.ProximoCapitulo = result.ProximoCapitulo;
            ViewBag.CapituloAnterior = result.CapituloAnterior;

            return View(result.PaginaAtual);
        }

        // GET: Manga/Favoritos
        [Authorize]
        public async Task<IActionResult> Favoritos()
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            var favoritos = await _favorites.GetFavoritesAsync(CurrentUserId());
            return View(favoritos);
        }

        // POST: Manga/AddFavorito/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFavorito(int id)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            await _favorites.AddAsync(CurrentUserId(), id);
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Manga/RemoveFavorito/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFavorito(int id, string? returnUrl)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            await _favorites.RemoveAsync(CurrentUserId(), id);

            if (!string.IsNullOrEmpty(returnUrl) && returnUrl == "favoritos")
            {
                return RedirectToAction(nameof(Favoritos));
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Manga/MarkChapterRead
        // Registra que o usuário leu um capítulo (chamado via AJAX do leitor / toggle manual).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkChapterRead(int mangaId, int capituloId)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            await _progress.MarkChapterReadAsync(CurrentUserId(), mangaId, capituloId);
            return Json(new { ok = true, lido = true });
        }

        // POST: Manga/UnmarkChapterRead
        // Desmarca um capítulo lido (toggle manual na página de detalhes).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnmarkChapterRead(int mangaId, int capituloId)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            await _progress.UnmarkChapterReadAsync(CurrentUserId(), capituloId);
            return Json(new { ok = true, lido = false });
        }

        // POST: Manga/AddReview
        // Creates the user's review or updates it if one already exists (one per manga).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int mangaId, int nota, string texto)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            await _reviews.AddOrUpdateReviewAsync(CurrentUserId(), mangaId, nota, texto);
            return RedirectToAction(nameof(Details), new { id = mangaId });
        }

        // POST: Manga/DeleteReview
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReview(int mangaId)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            await _reviews.DeleteReviewAsync(CurrentUserId(), mangaId);
            return RedirectToAction(nameof(Details), new { id = mangaId });
        }

        // POST: Manga/AddReply
        // Adds a reply (comment without rating) to a review.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReply(int mangaId, int reviewId, string texto)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            await _reviews.AddReplyAsync(CurrentUserId(), mangaId, reviewId, texto);
            return RedirectToAction(nameof(Details), new { id = mangaId });
        }

        // POST: Manga/DeleteReply
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReply(int mangaId, int replyId)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            await _reviews.DeleteReplyAsync(CurrentUserId(), replyId);
            return RedirectToAction(nameof(Details), new { id = mangaId });
        }
    }
}
