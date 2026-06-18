using System.ComponentModel.DataAnnotations;

namespace AnyComic.Models
{
    public class Usuario
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "The name is required")]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "The email is required")]
        [EmailAddress(ErrorMessage = "Invalid email")]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "The password is required")]
        [StringLength(255)]
        public string Senha { get; set; } = string.Empty;

        public DateTime DataCriacao { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Sobre { get; set; }

        [StringLength(260)]
        public string? FotoPerfil { get; set; }

        [StringLength(260)]
        public string? ImagemBanner { get; set; }

        // Relationship with Favoritos
        public ICollection<Favorito> Favoritos { get; set; } = new List<Favorito>();

        // Relationship with FavoritosAnime
        public ICollection<FavoritoAnime> FavoritosAnime { get; set; } = new List<FavoritoAnime>();

        // Relationship with manga reviews written by this user
        public ICollection<ReviewManga> ReviewsManga { get; set; } = new List<ReviewManga>();

        // Relationship with anime reviews written by this user
        public ICollection<ReviewAnime> ReviewsAnime { get; set; } = new List<ReviewAnime>();
    }
}
