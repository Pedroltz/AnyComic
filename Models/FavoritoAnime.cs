using System.ComponentModel.DataAnnotations;

namespace AnyComic.Models
{
    /// <summary>
    /// Represents an anime favorited by a user.
    /// Mirrors the <see cref="Favorito"/> entity used for mangas.
    /// </summary>
    public class FavoritoAnime
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }

        [Required]
        public int AnimeId { get; set; }

        public DateTime DataAdicao { get; set; } = DateTime.Now;

        // Relacionamentos
        public Usuario? Usuario { get; set; }
        public Anime? Anime { get; set; }
    }
}
