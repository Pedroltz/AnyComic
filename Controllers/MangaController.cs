using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AnyComic.Data;
using AnyComic.Models;
using AnyComic.Models.ViewModels;
using AnyComic.Services;

namespace AnyComic.Controllers
{
    public class MangaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public MangaController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: Manga/Index
        public async Task<IActionResult> Index(string? searchTerm, string? autor, DateTime? dataInicio, DateTime? dataFim, string? sortBy)
        {
            var query = _context.Mangas.AsQueryable();

            // Filtro por título
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(m => m.Titulo.Contains(searchTerm));
            }

            // Filtro por autor
            if (!string.IsNullOrWhiteSpace(autor))
            {
                query = query.Where(m => m.Autor.Contains(autor));
            }

            // Filtro por data de criação (início)
            if (dataInicio.HasValue)
            {
                query = query.Where(m => m.DataCriacao >= dataInicio.Value);
            }

            // Filtro por data de criação (fim)
            if (dataFim.HasValue)
            {
                query = query.Where(m => m.DataCriacao <= dataFim.Value);
            }

            // Ordenação
            query = sortBy switch
            {
                "titulo_asc" => query.OrderBy(m => m.Titulo),
                "titulo_desc" => query.OrderByDescending(m => m.Titulo),
                "autor_asc" => query.OrderBy(m => m.Autor),
                "autor_desc" => query.OrderByDescending(m => m.Autor),
                "data_asc" => query.OrderBy(m => m.DataCriacao),
                "data_desc" => query.OrderByDescending(m => m.DataCriacao),
                _ => query.OrderByDescending(m => m.DataCriacao) // Padrão: mais recentes primeiro
            };

            var mangas = await query
                .Include(m => m.Capitulos)
                .ToListAsync();

            // Passar os filtros atuais para a view
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Autor = autor;
            ViewBag.DataInicio = dataInicio?.ToString("yyyy-MM-dd");
            ViewBag.DataFim = dataFim?.ToString("yyyy-MM-dd");
            ViewBag.SortBy = sortBy;

            return View(mangas);
        }

        // GET: Manga/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var manga = await _context.Mangas
                .Include(m => m.Capitulos.OrderBy(c => c.NumeroCapitulo))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (manga == null)
            {
                return NotFound();
            }

            // Load only page counts per chapter (not full page entities)
            var pageCountsByChapter = await _context.PaginasMangas
                .Where(p => p.MangaId == id)
                .GroupBy(p => p.CapituloId)
                .Select(g => new { CapituloId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CapituloId, x => x.Count);

            ViewBag.PageCountsByChapter = pageCountsByChapter;

            // Total pages count for stats
            var totalPages = pageCountsByChapter.Values.Sum();
            ViewBag.TotalPages = totalPages;

            // Check if manga has any pages (for "Start Reading" button)
            ViewBag.HasPages = totalPages > 0;

            // Verificar se está nos favoritos do usuário
            bool isAuthenticated = User.Identity?.IsAuthenticated == true;
            bool isAdmin = User.FindFirstValue("IsAdmin") == "True";
            bool canReview = isAuthenticated && !isAdmin;
            int? currentUserId = null;
            if (canReview)
            {
                currentUserId = int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
                var isFavorito = await _context.Favoritos
                    .AnyAsync(f => f.UsuarioId == currentUserId && f.MangaId == id);
                ViewBag.IsFavorito = isFavorito;
            }

            // Carregar reviews da obra (mais recentes primeiro) com suas respostas
            var reviews = await _context.ReviewsManga
                .Include(r => r.Usuario)
                .Include(r => r.Replies).ThenInclude(rep => rep.Usuario)
                .Where(r => r.MangaId == id)
                .OrderByDescending(r => r.DataAtualizacao ?? r.DataCriacao)
                .ToListAsync();

            ViewBag.Reviews = BuildReviewsSection(reviews, currentUserId, id.Value, isAuthenticated, canReview);

            return View(manga);
        }

        /// <summary>
        /// Projects loaded manga reviews into the shared presentation view model.
        /// </summary>
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

        // GET: Manga/Read/5
        public async Task<IActionResult> Read(int? id, int? capituloNumero = null, string pagina = "1")
        {
            if (id == null)
            {
                return NotFound();
            }

            var manga = await _context.Mangas
                .Include(m => m.Capitulos.OrderBy(c => c.NumeroCapitulo))
                    .ThenInclude(c => c.Paginas.OrderBy(p => p.NumeroPagina))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (manga == null)
            {
                return NotFound();
            }

            if (!manga.Capitulos.Any())
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            // If no chapter specified, start from Chapter 1
            Capitulo? capituloAtual;
            if (capituloNumero == null)
            {
                capituloAtual = manga.Capitulos.OrderBy(c => c.NumeroCapitulo).First();
            }
            else
            {
                capituloAtual = manga.Capitulos.FirstOrDefault(c => c.NumeroCapitulo == capituloNumero);
                if (capituloAtual == null)
                {
                    capituloAtual = manga.Capitulos.OrderBy(c => c.NumeroCapitulo).First();
                }
            }

            // Chapters imported by the WeebCentral catalog sweep have no pages yet —
            // index them lazily the first time a reader opens the chapter.
            if (!string.IsNullOrEmpty(capituloAtual.FonteCapituloId) && !capituloAtual.Paginas.Any())
            {
                var proxyUrl = _configuration["WeebCentral:ProxyUrl"];
                var importer = new WeebCentralImporter(string.IsNullOrEmpty(proxyUrl) ? null : proxyUrl);
                var chapterDto = new WeebCentralImporter.WeebCentralChapter
                {
                    Id            = capituloAtual.FonteCapituloId,
                    ChapterNumber = capituloAtual.NumeroCapitulo,
                    ChapterTitle  = capituloAtual.NomeCapitulo ?? ""
                };

                var pageUrls = await importer.IndexChapterPages(chapterDto, string.Empty);
                if (pageUrls.Count > 0)
                {
                    int pageNumber = 1;
                    var novasPaginas = pageUrls.Select(url => new PaginaManga
                    {
                        MangaId       = manga.Id,
                        CapituloId    = capituloAtual.Id,
                        NumeroPagina  = pageNumber++,
                        CaminhoImagem = url,
                        DataUpload    = DateTime.Now
                    }).ToList();

                    _context.PaginasMangas.AddRange(novasPaginas);
                    await _context.SaveChangesAsync();

                    capituloAtual.Paginas = novasPaginas;
                }
            }

            if (!capituloAtual.Paginas.Any())
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            // Get the requested page from the current chapter
            PaginaManga? paginaAtual;
            if (pagina.Equals("last", StringComparison.OrdinalIgnoreCase))
            {
                paginaAtual = capituloAtual.Paginas.OrderByDescending(p => p.NumeroPagina).First();
            }
            else
            {
                int.TryParse(pagina, out int paginaNum);
                paginaAtual = capituloAtual.Paginas.FirstOrDefault(p => p.NumeroPagina == paginaNum);
                if (paginaAtual == null)
                {
                    paginaAtual = capituloAtual.Paginas.OrderBy(p => p.NumeroPagina).First();
                }
            }

            // Chapter-scoped navigation
            var paginasDoCapitulo = capituloAtual.Paginas.OrderBy(p => p.NumeroPagina).ToList();

            // Build page map only for the current chapter (includes image paths for client-side navigation)
            var pageMap = paginasDoCapitulo.Select((p, index) => new
            {
                pagina = p.NumeroPagina,
                index = index + 1,
                imagem = EnsureProxied(p.CaminhoImagem)
            }).ToList();

            // Determine next/previous chapters
            var capitulosOrdenados = manga.Capitulos.OrderBy(c => c.NumeroCapitulo).ToList();
            var capituloIndex = capitulosOrdenados.FindIndex(c => c.Id == capituloAtual.Id);
            var proximoCapitulo = capituloIndex < capitulosOrdenados.Count - 1
                ? capitulosOrdenados[capituloIndex + 1] : null;
            var capituloAnterior = capituloIndex > 0
                ? capitulosOrdenados[capituloIndex - 1] : null;

            ViewBag.TotalPaginas = paginasDoCapitulo.Count;
            ViewBag.PaginaAtual = paginaAtual.NumeroPagina;
            ViewBag.PaginaImagemUrl = EnsureProxied(paginaAtual.CaminhoImagem);
            ViewBag.MangaId = manga.Id;
            ViewBag.MangaTitulo = manga.Titulo;
            ViewBag.CapituloAtual = capituloAtual;
            ViewBag.TotalCapitulos = manga.Capitulos.Count;
            ViewBag.PageMap = pageMap;
            ViewBag.Capitulos = capitulosOrdenados;
            ViewBag.ProximoCapitulo = proximoCapitulo;
            ViewBag.CapituloAnterior = capituloAnterior;

            return View(paginaAtual);
        }

        // GET: Manga/Favoritos
        [Authorize]
        public async Task<IActionResult> Favoritos()
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            var userId = int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);

            var favoritos = await _context.Favoritos
                .Include(f => f.Manga)
                .Where(f => f.UsuarioId == userId)
                .OrderByDescending(f => f.DataAdicao)
                .ToListAsync();

            return View(favoritos);
        }

        // POST: Manga/AddFavorito/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFavorito(int id)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            var userId = int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);

            var favoritoExistente = await _context.Favoritos
                .FirstOrDefaultAsync(f => f.UsuarioId == userId && f.MangaId == id);

            if (favoritoExistente == null)
            {
                var favorito = new Favorito
                {
                    UsuarioId = userId,
                    MangaId = id,
                    DataAdicao = DateTime.Now
                };

                _context.Favoritos.Add(favorito);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Manga/RemoveFavorito/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFavorito(int id, string? returnUrl)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            var userId = int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);

            var favorito = await _context.Favoritos
                .FirstOrDefaultAsync(f => f.UsuarioId == userId && f.MangaId == id);

            if (favorito != null)
            {
                _context.Favoritos.Remove(favorito);
                await _context.SaveChangesAsync();
            }

            if (!string.IsNullOrEmpty(returnUrl) && returnUrl == "favoritos")
            {
                return RedirectToAction(nameof(Favoritos));
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Manga/AddReview
        // Creates the user's review or updates it if one already exists (one per manga).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int mangaId, int nota, string texto)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            var userId = int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);

            if (nota < 1 || nota > 5 || string.IsNullOrWhiteSpace(texto))
            {
                return RedirectToAction(nameof(Details), new { id = mangaId });
            }

            texto = texto.Trim();
            if (texto.Length > 2000) texto = texto[..2000];

            var review = await _context.ReviewsManga
                .FirstOrDefaultAsync(r => r.UsuarioId == userId && r.MangaId == mangaId);

            if (review == null)
            {
                _context.ReviewsManga.Add(new ReviewManga
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

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = mangaId });
        }

        // POST: Manga/DeleteReview
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReview(int mangaId)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            var userId = int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);

            var review = await _context.ReviewsManga
                .FirstOrDefaultAsync(r => r.UsuarioId == userId && r.MangaId == mangaId);

            if (review != null)
            {
                _context.ReviewsManga.Remove(review);
                await _context.SaveChangesAsync();
            }

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

            var userId = int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);

            if (string.IsNullOrWhiteSpace(texto))
            {
                return RedirectToAction(nameof(Details), new { id = mangaId });
            }

            texto = texto.Trim();
            if (texto.Length > 2000) texto = texto[..2000];

            // Ensure the review exists and belongs to this manga before replying.
            var reviewExists = await _context.ReviewsManga
                .AnyAsync(r => r.Id == reviewId && r.MangaId == mangaId);

            if (reviewExists)
            {
                _context.ReviewRepliesManga.Add(new ReviewReplyManga
                {
                    ReviewId    = reviewId,
                    UsuarioId   = userId,
                    Texto       = texto,
                    DataCriacao = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = mangaId });
        }

        // POST: Manga/DeleteReply
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReply(int mangaId, int replyId)
        {
            if (User.FindFirstValue("IsAdmin") == "True") return Forbid();

            var userId = int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);

            var reply = await _context.ReviewRepliesManga
                .FirstOrDefaultAsync(r => r.Id == replyId && r.UsuarioId == userId);

            if (reply != null)
            {
                _context.ReviewRepliesManga.Remove(reply);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = mangaId });
        }

        /// <summary>
        /// Wraps external CDN image URLs through the local proxy so hotlink protection is bypassed.
        /// Leaves local paths and already-proxied URLs untouched.
        /// </summary>
        private static string EnsureProxied(string url)
        {
            if (string.IsNullOrEmpty(url) || url.StartsWith("/Proxy/") || url.StartsWith("/uploads/") || url.StartsWith("/images/"))
                return url;

            if (url.StartsWith("https://uploads.mangadex.org") || url.StartsWith("http://uploads.mangadex.org"))
                return $"/Proxy/Image?url={Uri.EscapeDataString(url)}";

            return url;
        }
    }
}
