using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FamilyTree.Models;
using FamilyTree.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly FamilyTreeDbContext _context;

    public HomeController(ILogger<HomeController> logger, FamilyTreeDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    [Authorize]
    public async Task<IActionResult> Index()
    {
        var setting = await _context.SystemSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        ViewData["FamilyName"] = setting?.FamilyName ?? "周氏家谱";
        ViewData["SiteTitle"] = setting?.SiteTitle ?? "族谱管理系统";
        ViewData["Announcement"] = setting?.Announcement ?? string.Empty;
        return View();
    }

    [Authorize]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}