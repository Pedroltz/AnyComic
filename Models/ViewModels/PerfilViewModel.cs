using System.ComponentModel.DataAnnotations;

namespace AnyComic.Models.ViewModels
{
    public class PerfilViewModel
    {
        public Usuario Usuario { get; set; } = null!;
        public List<Favorito> FavoritosManga { get; set; } = new();
        public List<FavoritoAnime> FavoritosAnime { get; set; } = new();
        public bool IsOwnProfile { get; set; }
    }

    public class EditarPerfilViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name must be at most 100 characters")]
        public string Nome { get; set; } = "";

        public string? Sobre { get; set; }
        public IFormFile? FotoPerfil { get; set; }
        public IFormFile? ImagemBanner { get; set; }
        public string? FotoPerfilAtual { get; set; }
        public string? ImagemBannerAtual { get; set; }
    }
}
