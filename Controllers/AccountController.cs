using System.Security.Claims;
using FamilyTree.Data;
using FamilyTree.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Controllers;

public class AccountController : Controller
{
    private readonly FamilyTreeDbContext _context;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public AccountController(FamilyTreeDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username.Trim());
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "用户名或密码错误");
            return View(model);
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "用户名或密码错误");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpGet]
    public async Task<IActionResult> Users()
    {
        ViewData["Users"] = await _context.Users
            .OrderBy(u => u.Username)
            .ToListAsync();

        return View(new CreateUserViewModel { Role = UserRole.Viewer });
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Users(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Users"] = await _context.Users.OrderBy(u => u.Username).ToListAsync();
            return View(model);
        }

        var username = model.Username.Trim();
        var exists = await _context.Users.AnyAsync(u => u.Username == username);
        if (exists)
        {
            ModelState.AddModelError(nameof(CreateUserViewModel.Username), "用户名已存在");
            ViewData["Users"] = await _context.Users.OrderBy(u => u.Username).ToListAsync();
            return View(model);
        }

        var user = new AppUser
        {
            Username = username,
            Role = model.Role,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        TempData["Success"] = "账号已创建";
        return RedirectToAction(nameof(Users));
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var entity = await _context.SystemSettings.OrderBy(s => s.Id).FirstOrDefaultAsync()
                     ?? new SystemSetting();

        var model = new SystemSettingViewModel
        {
            FamilyName = entity.FamilyName,
            SiteTitle = entity.SiteTitle,
            ContactName = entity.ContactName,
            ContactPhone = entity.ContactPhone,
            ContactEmail = entity.ContactEmail,
            FamilyOrigin = entity.FamilyOrigin,
            Announcement = entity.Announcement,
            PrimaryColor = entity.PrimaryColor,
            EnableShare = entity.EnableShare,
            EnablePublicView = entity.EnablePublicView,
            ShowCardRank = entity.ShowCardRank,
            ShowCardGeneration = entity.ShowCardGeneration,
            ShowCardGenerationLevel = entity.ShowCardGenerationLevel,
            DefaultZoomPercent = entity.DefaultZoomPercent,
            LayoutOrientation = string.IsNullOrWhiteSpace(entity.LayoutOrientation)
                ? TreeLayoutOrientation.Horizontal
                : entity.LayoutOrientation
        };

        return View(model);
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(SystemSettingViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var entity = await _context.SystemSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (entity == null)
        {
            entity = new SystemSetting();
            _context.SystemSettings.Add(entity);
        }

        entity.FamilyName = model.FamilyName.Trim();
        entity.SiteTitle = model.SiteTitle.Trim();
        entity.ContactName = model.ContactName?.Trim();
        entity.ContactPhone = model.ContactPhone?.Trim();
        entity.ContactEmail = model.ContactEmail?.Trim();
        entity.FamilyOrigin = model.FamilyOrigin?.Trim();
        entity.Announcement = model.Announcement?.Trim();
        entity.PrimaryColor = string.IsNullOrWhiteSpace(model.PrimaryColor) ? "#dc3545" : model.PrimaryColor.Trim();
        entity.EnableShare = model.EnableShare;
        entity.EnablePublicView = model.EnablePublicView;
        entity.ShowCardRank = model.ShowCardRank;
        entity.ShowCardGeneration = model.ShowCardGeneration;
        entity.ShowCardGenerationLevel = model.ShowCardGenerationLevel;
        entity.DefaultZoomPercent = model.DefaultZoomPercent;
        entity.LayoutOrientation = model.LayoutOrientation == TreeLayoutOrientation.Horizontal
            ? TreeLayoutOrientation.Horizontal
            : TreeLayoutOrientation.Vertical;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        TempData["Success"] = "设置已保存";
        return RedirectToAction(nameof(Settings));
    }
}

