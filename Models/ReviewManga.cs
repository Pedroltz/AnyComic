using System.ComponentModel.DataAnnotations;

namespace AnyComic.Models
{
    /// <summary>
    /// Represents a written review with a star rating left by a user on a manga.
    /// Each user can have at most one review per manga (enforced by a unique index).
    /// </summary>
    public class ReviewManga
    {
        /// <summary>
        /// Unique identifier of the review (primary key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the author of the review
        /// </summary>
        [Required]
        public int UsuarioId { get; set; }

        /// <summary>
        /// Foreign key to the reviewed manga
        /// </summary>
        [Required]
        public int MangaId { get; set; }

        /// <summary>
        /// Star rating from 1 to 5
        /// </summary>
        [Required]
        [Range(1, 5, ErrorMessage = "The rating must be between 1 and 5 stars")]
        public int Nota { get; set; }

        /// <summary>
        /// Written feedback (required, maximum 2000 characters)
        /// </summary>
        [Required(ErrorMessage = "The review text is required")]
        [StringLength(2000, ErrorMessage = "The review must be at most 2000 characters")]
        public string Texto { get; set; } = string.Empty;

        /// <summary>
        /// Date when the review was first created
        /// </summary>
        public DateTime DataCriacao { get; set; } = DateTime.Now;

        /// <summary>
        /// Date of the last edit, when the review has been updated
        /// </summary>
        public DateTime? DataAtualizacao { get; set; }

        // Relationships (Navigation Properties)

        public Usuario? Usuario { get; set; }
        public Manga? Manga { get; set; }

        /// <summary>
        /// Replies (comments) left on this review. No rating attached.
        /// </summary>
        public ICollection<ReviewReplyManga> Replies { get; set; } = new List<ReviewReplyManga>();
    }
}
