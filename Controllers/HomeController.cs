using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AnyComic.Models;
using AnyComic.Application.Home;

namespace AnyComic.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IHomeService _home;

    public HomeController(ILogger<HomeController> logger, IHomeService home)
    {
        _logger = logger;
        _home = home;
    }

    public async Task<IActionResult> Index()
    {
        var viewModel = await _home.GetHomeAsync();
        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
