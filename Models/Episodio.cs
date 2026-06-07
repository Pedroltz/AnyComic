using System.ComponentModel.DataAnnotations;

namespace AnyComic.Models
{
    /// <summary>
    /// Represents an episode within an anime.
    /// Mirrors the <see cref="Capitulo"/> entity, but each episode holds a
    /// single video link/file instead of a collection of pages.
    /// </summary>
    public class Episodio
    {
        /// <summary>
        /// Unique identifier of the episode (primary key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the associated anime
        /// </summary>
        [Required]
        public int AnimeId { get; set; }

        /// <summary>
        /// Sequential episode number (1, 2, 3, ...)
        /// </summary>
        [Required]
        public int NumeroEpisodio { get; set; }

        /// <summary>
        /// Custom episode name (optional)
        /// If empty, will display as "Episode X"
        /// </summary>
        [StringLength(200)]
        public string? NomeEpisodio { get; set; }

        /// <summary>
        /// Video link or relative file path for this episode.
        /// Can be an embed/stream URL (e.g. YouTube) or an uploaded file path.
        /// </summary>
        [Required(ErrorMessage = "The video link is required")]
        [StringLength(1000)]
        public string LinkVideo { get; set; } = string.Empty;

        /// <summary>
        /// Release date of the episode
        /// </summary>
        public DateTime DataLancamento { get; set; } = DateTime.Now;

        /// <summary>
        /// Date when the episode was created/uploaded
        /// </summary>
        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Relationships (Navigation Properties)

        /// <summary>
        /// The anime this episode belongs to
        /// </summary>
        public Anime? Anime { get; set; }

        /// <summary>
        /// Gets the display name for this episode
        /// Returns custom name if set, otherwise "Episode X"
        /// </summary>
        public string NomeExibicao => string.IsNullOrWhiteSpace(NomeEpisodio)
            ? $"Episode {NumeroEpisodio}"
            : NomeEpisodio;
    }
}
