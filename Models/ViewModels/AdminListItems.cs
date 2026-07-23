namespace AnyComic.Models.ViewModels;

public class MangaListItem
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public string ImagemCapa { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public int PaginasCount { get; set; }
}

public class AnimeListItem
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public string ImagemCapa { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public int EpisodiosCount { get; set; }
}
