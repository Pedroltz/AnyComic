namespace AnyComic.Models.ViewModels;

public enum PlayerKind { None, File, YouTube, Vimeo, Iframe }

public class PlayerInfo
{
    public PlayerKind Kind     { get; init; } = PlayerKind.None;
    public string     EmbedUrl { get; init; } = string.Empty;
    public string?    YoutubeId { get; init; }
    public string?    VimeoId   { get; init; }
}
