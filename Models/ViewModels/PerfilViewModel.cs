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
        public string? Sobre { get; set; }
        public IFormFile? FotoPerfil { get; set; }
        public IFormFile? ImagemBanner { get; set; }
        public string? FotoPerfilAtual { get; set; }
        public string? ImagemBannerAtual { get; set; }
        public string Nome { get; set; } = "";
    }
}
