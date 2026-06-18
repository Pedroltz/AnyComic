namespace AnyComic.Models.ViewModels;

public class AnimeSearchFilter
{
    public string?   SearchTerm { get; set; }
    public string?   Autor      { get; set; }
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim    { get; set; }
    public string?   SortBy     { get; set; }
}
