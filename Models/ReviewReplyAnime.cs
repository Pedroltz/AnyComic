using System.ComponentModel.DataAnnotations;

namespace AnyComic.Models
{
    /// <summary>
    /// A reply to a <see cref="ReviewAnime"/>. Mirrors <see cref="ReviewReplyManga"/>.
    /// Replies are plain comments — no star rating, and a user may post multiple
    /// replies to a review.
    /// </summary>
    public class ReviewReplyAnime
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the review this reply belongs to
        /// </summary>
        [Required]
        public int ReviewId { get; set; }

        /// <summary>
        /// Foreign key to the author of the reply
        /// </summary>
        [Required]
        public int UsuarioId { get; set; }

        /// <summary>
        /// Reply text (required, maximum 2000 characters)
        /// </summary>
        [Required(ErrorMessage = "The reply text is required")]
        [StringLength(2000, ErrorMessage = "The reply must be at most 2000 characters")]
        public string Texto { get; set; } = string.Empty;

        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public DateTime? DataAtualizacao { get; set; }

        // Relationships (Navigation Properties)

        public Usuario? Usuario { get; set; }
        public ReviewAnime? Review { get; set; }
    }
}
