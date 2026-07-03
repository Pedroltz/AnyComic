using System.Text.Json.Serialization;
using AnyComic.Models;
using AnyComic.Models.ViewModels;

namespace AnyComic.Application.Mangas;

/// <summary>Everything the Manga/Details view needs, computed in the application layer.</summary>
public class MangaDetailsResult
{
    public Manga Manga { get; set; } = null!;
    public Dictionary<int, int> PageCountsByChapter { get; set; } = new();
    public int TotalPages { get; set; }
    public bool HasPages { get; set; }
    public bool IsFavorito { get; set; }
    public ReviewsSectionViewModel Reviews { get; set; } = null!;
}

public enum MangaReaderStatus
{
    Success,
    NotFound,
    RedirectToDetails
}

/// <summary>Everything the Manga/Read view needs (or a redirect/not-found signal).</summary>
public class MangaReaderResult
{
    public MangaReaderStatus Status { get; set; }
    public int MangaId { get; set; }

    public PaginaManga? PaginaAtual { get; set; }
    public int TotalPaginas { get; set; }
    public int PaginaNumero { get; set; }
    public string PaginaImagemUrl { get; set; } = string.Empty;
    public string MangaTitulo { get; set; } = string.Empty;
    public Capitulo? CapituloAtual { get; set; }
    public int TotalCapitulos { get; set; }
    public IReadOnlyList<ReaderPage> PageMap { get; set; } = new List<ReaderPage>();
    public List<Capitulo> Capitulos { get; set; } = new();
    public Capitulo? ProximoCapitulo { get; set; }
    public Capitulo? CapituloAnterior { get; set; }

    public static MangaReaderResult NotFoundResult() => new() { Status = MangaReaderStatus.NotFound };
    public static MangaReaderResult Redirect(int mangaId) =>
        new() { Status = MangaReaderStatus.RedirectToDetails, MangaId = mangaId };
}

/// <summary>
/// Single entry of the client-side page map. Property names are lowercased for the JSON the
/// reader's JavaScript consumes (<c>p.pagina</c>, <c>p.imagem</c>).
/// </summary>
public record ReaderPage(
    [property: JsonPropertyName("pagina")] int Pagina,
    [property: JsonPropertyName("index")]  int Index,
    [property: JsonPropertyName("imagem")] string Imagem);
