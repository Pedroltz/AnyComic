using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AnyComic.Data;
using AnyComic.Models;
using AnyComic.Models.ViewModels;

namespace AnyComic.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        private static readonly HashSet<string> _allowedExts =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        public ProfileController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET /Profile/{id}
        [HttpGet("Profile/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Index(int id)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Favoritos).ThenInclude(f => f.Manga)
                .Include(u => u.FavoritosAnime).ThenInclude(f => f.Anime)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var vm = new PerfilViewModel
            {
                Usuario = usuario,
                FavoritosManga = usuario.Favoritos
                    .Where(f => f.Manga != null)
                    .OrderByDescending(f => f.DataAdicao)
                    .Take(24)
                    .ToList(),
                FavoritosAnime = usuario.FavoritosAnime
                    .Where(f => f.Anime != null)
                    .OrderByDescending(f => f.DataAdicao)
                    .Take(24)
                    .ToList(),
                IsOwnProfile = currentUserId == id.ToString()
            };

            return View(vm);
        }

        // GET /Profile/Edit
        [Authorize]
        public async Task<IActionResult> Edit()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var usuario = await _context.Usuarios.FindAsync(userId);
            if (usuario == null) return NotFound();

            return View(new EditarPerfilViewModel
            {
                Sobre = usuario.Sobre,
                FotoPerfilAtual = usuario.FotoPerfil,
                ImagemBannerAtual = usuario.ImagemBanner
            });
        }

        // POST /Profile/Edit
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditarPerfilViewModel vm)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var usuario = await _context.Usuarios.FindAsync(userId);
            if (usuario == null) return NotFound();

            usuario.Sobre = vm.Sobre?.Trim();

            if (vm.FotoPerfil != null && vm.FotoPerfil.Length > 0)
            {
                var result = await SalvarImagem(vm.FotoPerfil, $"{userId}_avatar", 5 * 1024 * 1024);
                if (result is null)
                {
                    ModelState.AddModelError("FotoPerfil", "Invalid file. Use JPG, PNG, WEBP or GIF up to 5 MB.");
                    vm.FotoPerfilAtual = usuario.FotoPerfil;
                    vm.ImagemBannerAtual = usuario.ImagemBanner;
                    return View(vm);
                }
                usuario.FotoPerfil = result;
            }

            if (vm.ImagemBanner != null && vm.ImagemBanner.Length > 0)
            {
                var result = await SalvarImagem(vm.ImagemBanner, $"{userId}_banner", 10 * 1024 * 1024);
                if (result is null)
                {
                    ModelState.AddModelError("ImagemBanner", "Invalid file. Use JPG, PNG, WEBP or GIF up to 10 MB.");
                    vm.FotoPerfilAtual = usuario.FotoPerfil;
                    vm.ImagemBannerAtual = usuario.ImagemBanner;
                    return View(vm);
                }
                usuario.ImagemBanner = result;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { id = userId });
        }

        private async Task<string?> SalvarImagem(IFormFile file, string nome, long maxBytes)
        {
            if (file.Length > maxBytes) return null;

            var ext = Path.GetExtension(file.FileName);
            if (!_allowedExts.Contains(ext)) return null;

            var dir = Path.Combine(_env.WebRootPath, "uploads", "perfis");
            Directory.CreateDirectory(dir);

            // Remove versão anterior do mesmo arquivo (qualquer extensão)
            foreach (var old in Directory.GetFiles(dir, $"{nome}.*"))
                System.IO.File.Delete(old);

            var fileName = $"{nome}{ext}";
            await using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/perfis/{fileName}";
        }
    }
}
