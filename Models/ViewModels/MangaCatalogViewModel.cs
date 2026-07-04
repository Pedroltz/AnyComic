namespace AnyComic.Models.ViewModels;

/// <summary>
/// Paged catalog listing for the Manga/Index page. Carries only the fields the grid
/// renders (no full chapter entities) plus pagination metadata and the active filters,
/// so page links can preserve the current search/sort.
/// </summary>
public class MangaCatalogViewModel
{
    public List<MangaCardViewModel> Mangas { get; set; } = new();

    // Pagination
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    // Active filters (echoed so pagination links keep them)
    public string?   SearchTerm { get; set; }
    public string?   Autor      { get; set; }
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim    { get; set; }
    public string?   SortBy     { get; set; }
}

/// <summary>Lightweight per-card projection for the catalog grid.</summary>
public class MangaCardViewModel
{
    public int      Id           { get; set; }
    public string   Titulo       { get; set; } = string.Empty;
    public string   Autor        { get; set; } = string.Empty;
    public string?  ImagemCapa   { get; set; }
    public DateTime DataCriacao  { get; set; }
    public int      ChapterCount { get; set; }
}
