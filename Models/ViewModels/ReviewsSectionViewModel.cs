namespace AnyComic.Models.ViewModels
{
    /// <summary>
    /// A single review projected into a presentation-friendly shape so the
    /// shared reviews partial can render manga and anime reviews uniformly.
    /// </summary>
    public class ReviewItemViewModel
    {
        public int ReviewId { get; set; }
        public int UsuarioId { get; set; }
        public string UsuarioNome { get; set; } = string.Empty;
        public string? UsuarioFoto { get; set; }
        public int Nota { get; set; }
        public string Texto { get; set; } = string.Empty;
        public DateTime Data { get; set; }
        public bool Editado { get; set; }

        /// <summary>Replies to this review, oldest first.</summary>
        public List<ReviewReplyItemViewModel> Replies { get; set; } = new();
    }

    /// <summary>
    /// A reply (comment without a rating) projected for the shared partial.
    /// </summary>
    public class ReviewReplyItemViewModel
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string UsuarioNome { get; set; } = string.Empty;
        public string? UsuarioFoto { get; set; }
        public string Texto { get; set; } = string.Empty;
        public DateTime Data { get; set; }
        public bool Editado { get; set; }
    }

    /// <summary>
    /// Drives the shared <c>_ReviewsSection</c> partial rendered at the bottom
    /// of the manga and anime details pages.
    /// </summary>
    public class ReviewsSectionViewModel
    {
        /// <summary>Controller that owns the review actions ("Manga" or "Anime").</summary>
        public string Controller { get; set; } = string.Empty;

        /// <summary>Id of the manga/anime being reviewed.</summary>
        public int WorkId { get; set; }

        /// <summary>All reviews for the work, most recent first.</summary>
        public List<ReviewItemViewModel> Reviews { get; set; } = new();

        /// <summary>The current user's own review, if they have one.</summary>
        public ReviewItemViewModel? UserReview { get; set; }

        /// <summary>Whether a user is signed in (controls the form vs. sign-in prompt).</summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>Current user's id, used to show delete controls on their own replies.</summary>
        public int? CurrentUserId { get; set; }
    }
}
