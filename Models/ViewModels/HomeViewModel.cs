using AnyComic.Models;

namespace AnyComic.Models.ViewModels;

/// <summary>Everything the home page renders: the banner carousel plus a set of manga rows.</summary>
public class HomeViewModel
{
    public List<Banner> Banners { get; set; } = new();
    public List<HomeSectionViewModel> Sections { get; set; } = new();
}

/// <summary>A titled horizontal row of manga cards on the home page.</summary>
public class HomeSectionViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    /// <summary>Icon keyword the view maps to an SVG: new, clock, fire, star, shuffle.</summary>
    public string Icon { get; set; } = "new";
    /// <summary>Optional "View All" link controller (e.g. "Manga"); null hides the link.</summary>
    public string? ViewAllController { get; set; }
    /// <summary>When true, render as a multi-row grid; otherwise a single horizontal scroll row.</summary>
    public bool Grid { get; set; }
    public List<MangaCardViewModel> Items { get; set; } = new();
}
