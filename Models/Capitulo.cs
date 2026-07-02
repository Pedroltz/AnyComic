using System.ComponentModel.DataAnnotations;

namespace AnyComic.Models
{
    /// <summary>
    /// Represents a chapter within a manga.
    /// Allows organizing manga pages into chapters.
    /// </summary>
    public class Capitulo
    {
        /// <summary>
        /// Unique identifier of the chapter (primary key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the associated manga
        /// </summary>
        [Required]
        public int MangaId { get; set; }

        /// <summary>
        /// Sequential chapter number (1, 2, 3, ...)
        /// </summary>
        [Required]
        public int NumeroCapitulo { get; set; }

        /// <summary>
        /// Custom chapter name (optional)
        /// If empty, will display as "Chapter X"
        /// </summary>
        [StringLength(200)]
        public string? NomeCapitulo { get; set; }

        /// <summary>
        /// Date when the chapter was created/uploaded
        /// </summary>
        public DateTime DataCriacao { get; set; } = DateTime.Now;

        /// <summary>
        /// Identifier of this chapter on the external source (e.g. WeebCentral chapter ID).
        /// Null for chapters with no known source, or whose pages are already indexed.
        /// Used to lazily index pages the first time a chapter is opened.
        /// </summary>
        [StringLength(100)]
        public string? FonteCapituloId { get; set; }

        // Relationships (Navigation Properties)

        /// <summary>
        /// The manga this chapter belongs to
        /// </summary>
        public Manga? Manga { get; set; }

        /// <summary>
        /// Collection of pages in this chapter
        /// 1:N relationship (one chapter has many pages)
        /// </summary>
        public ICollection<PaginaManga> Paginas { get; set; } = new List<PaginaManga>();

        /// <summary>
        /// Gets the display name for this chapter
        /// Returns custom name if set, otherwise "Chapter X"
        /// </summary>
        public string NomeExibicao => string.IsNullOrWhiteSpace(NomeCapitulo)
            ? $"Chapter {NumeroCapitulo}"
            : NomeCapitulo;
    }
}
