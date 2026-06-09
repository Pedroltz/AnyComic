using System.ComponentModel.DataAnnotations;

namespace AnyComic.Models
{
    /// <summary>
    /// Represents an anime in the system.
    /// Mirrors the <see cref="Manga"/> entity for the administrative CRUD.
    /// </summary>
    public class Anime
    {
        /// <summary>
        /// Unique identifier of the anime (primary key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Title of the anime (required, maximum 200 characters)
        /// </summary>
        [Required(ErrorMessage = "The title is required")]
        [StringLength(200)]
        public string Titulo { get; set; } = string.Empty;

        /// <summary>
        /// Name of the studio/author of the anime (required, maximum 100 characters)
        /// </summary>
        [Required(ErrorMessage = "The studio is required")]
        [StringLength(100)]
        public string Autor { get; set; } = string.Empty;

        /// <summary>
        /// Description or synopsis of the anime (required, maximum 1000 characters)
        /// </summary>
        [Required(ErrorMessage = "The description is required")]
        [StringLength(1000)]
        public string Descricao { get; set; } = string.Empty;

        /// <summary>
        /// Relative path of the cover image (maximum 500 characters)
        /// Example: /uploads/capas/imagem.jpg
        /// </summary>
        [StringLength(500)]
        public string ImagemCapa { get; set; } = string.Empty;

        /// <summary>
        /// Release date of the anime (provided by the administrator)
        /// </summary>
        [Required(ErrorMessage = "The release date is required")]
        public DateTime DataCriacao { get; set; }

        // Relationships (Navigation Properties)

        /// <summary>
        /// Collection of episodes associated with this anime
        /// 1:N relationship (one anime has many episodes)
        /// </summary>
        public ICollection<Episodio> Episodios { get; set; } = new List<Episodio>();

        /// <summary>
        /// Collection of favorites associated with this anime
        /// N:N relationship through the FavoritosAnime table
        /// </summary>
        public ICollection<FavoritoAnime> Favoritos { get; set; } = new List<FavoritoAnime>();

        /// <summary>
        /// Collection of user reviews left on this anime
        /// 1:N relationship (one anime has many reviews)
        /// </summary>
        public ICollection<ReviewAnime> Reviews { get; set; } = new List<ReviewAnime>();
    }
}
