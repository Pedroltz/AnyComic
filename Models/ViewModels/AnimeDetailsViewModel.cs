namespace AnyComic.Models.ViewModels;

public class AnimeDetailsViewModel
{
    public required Anime                Anime            { get; init; }
    public IReadOnlyList<Episodio>       Episodios        { get; init; } = [];
    public Episodio?                     EpisodioAtual    { get; init; }
    public Episodio?                     ProximoEpisodio  { get; init; }
    public Episodio?                     EpisodioAnterior { get; init; }
    public bool                          IsFavorito       { get; init; }
    public PlayerInfo                    Player           { get; init; } = new();
    public ReviewsSectionViewModel       Reviews          { get; init; } = new();
}
