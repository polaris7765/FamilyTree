using FamilyTree.Data;
using FamilyTree.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Add EF Core with SQLite
builder.Services.AddDbContext<FamilyTreeDbContext>(options =>
    options.UseSqlite("Data Source=familytree.db"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-migrate database and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FamilyTreeDbContext>();
    db.Database.EnsureCreated();

    // Ensure newly introduced auth table exists for pre-existing SQLite files.
    db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS Users (
    Id INTEGER NOT NULL CONSTRAINT PK_Users PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,
    Role INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL
);
");

    db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Username ON Users (Username);");

    db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS SystemSettings (
    Id INTEGER NOT NULL CONSTRAINT PK_SystemSettings PRIMARY KEY AUTOINCREMENT,
    FamilyName TEXT NOT NULL,
    SiteTitle TEXT NOT NULL,
    ContactName TEXT NULL,
    ContactPhone TEXT NULL,
    ContactEmail TEXT NULL,
    FamilyOrigin TEXT NULL,
    Announcement TEXT NULL,
    PrimaryColor TEXT NOT NULL,
    EnableShare INTEGER NOT NULL,
    EnablePublicView INTEGER NOT NULL,
    ShowCardRank INTEGER NOT NULL DEFAULT 1,
    ShowCardGeneration INTEGER NOT NULL DEFAULT 1,
    ShowCardGenerationLevel INTEGER NOT NULL DEFAULT 1,
    DefaultZoomPercent INTEGER NOT NULL,
    LayoutOrientation TEXT NOT NULL DEFAULT 'Horizontal',
    UpdatedAt TEXT NOT NULL
);
");

    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Persons ADD COLUMN MaleRank INTEGER NULL;");
    }
    catch
    {
        // Column already exists in upgraded environments.
    }

    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Persons ADD COLUMN Occupation TEXT NULL;");
    }
    catch
    {
        // Column already exists in upgraded environments.
    }

    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE SystemSettings ADD COLUMN LayoutOrientation TEXT NOT NULL DEFAULT 'Horizontal';");
    }
    catch
    {
        // Column already exists in upgraded environments.
    }

    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE SystemSettings ADD COLUMN ShowCardRank INTEGER NOT NULL DEFAULT 1;");
    }
    catch
    {
        // Column already exists in upgraded environments.
    }

    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE SystemSettings ADD COLUMN ShowCardGeneration INTEGER NOT NULL DEFAULT 1;");
    }
    catch
    {
        // Column already exists in upgraded environments.
    }

    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE SystemSettings ADD COLUMN ShowCardGenerationLevel INTEGER NOT NULL DEFAULT 1;");
    }
    catch
    {
        // Column already exists in upgraded environments.
    }

    // Fallback seed for environments where the DB already exists but has no data.
    if (!db.Persons.Any())
    {
        var now = DateTime.UtcNow;
        var root = new Person
        {
            Name = "周象民",
            Gender = Gender.Male,
            Generation = "一",
            IsRoot = true,
            BirthDate = new DateTime(1950, 1, 1),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Persons.Add(root);
        db.SaveChanges();

        var spouse = new Person
        {
            Name = "陈氏",
            Gender = Gender.Female,
            Generation = "一",
            SpouseId = root.Id,
            BirthDate = new DateTime(1952, 5, 1),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Persons.Add(spouse);
        db.SaveChanges();

        root.SpouseId = spouse.Id;

        db.Persons.AddRange(
            new Person
            {
                Name = "周长子",
                Gender = Gender.Male,
                Generation = "二",
                ParentId = root.Id,
                BirthDate = new DateTime(1975, 3, 12),
                CreatedAt = now,
                UpdatedAt = now
            },
            new Person
            {
                Name = "周长女",
                Gender = Gender.Female,
                Generation = "二",
                ParentId = root.Id,
                BirthDate = new DateTime(1978, 7, 8),
                CreatedAt = now,
                UpdatedAt = now
            }
        );

        db.SaveChanges();
    }

    var renamed = false;
    var fengMembers = db.Persons.Where(p => p.Name.StartsWith("冯")).ToList();
    foreach (var member in fengMembers)
    {
        member.Name = "周" + member.Name[1..];
        member.UpdatedAt = DateTime.UtcNow;
        renamed = true;
    }

    if (renamed)
        db.SaveChanges();

    var inferredOccupationUpdated = false;
    foreach (var member in db.Persons.Where(p => p.Name.Contains("农民") && p.Occupation != "农民"))
    {
        member.Occupation = "农民";
        member.UpdatedAt = DateTime.UtcNow;
        inferredOccupationUpdated = true;
    }

    if (inferredOccupationUpdated)
        db.SaveChanges();

    if (!db.Users.Any())
    {
        var adminUsername = builder.Configuration["AuthSettings:DefaultAdminUsername"] ?? "admin";
        var adminPassword = builder.Configuration["AuthSettings:DefaultAdminPassword"] ?? "123456";
        var hasher = new PasswordHasher<AppUser>();
        var admin = new AppUser
        {
            Username = adminUsername,
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
        db.Users.Add(admin);
        db.SaveChanges();
    }

    if (!db.SystemSettings.Any())
    {
        db.SystemSettings.Add(new SystemSetting
        {
            FamilyName = "周氏家谱",
            SiteTitle = "族谱管理系统",
            ContactName = "族谱管理员",
            ContactPhone = "",
            ContactEmail = "",
            FamilyOrigin = "",
            Announcement = "欢迎使用族谱管理系统",
            PrimaryColor = "#dc3545",
            EnableShare = true,
            EnablePublicView = false,
            ShowCardRank = true,
            ShowCardGeneration = true,
            ShowCardGenerationLevel = true,
            DefaultZoomPercent = 70,
            LayoutOrientation = TreeLayoutOrientation.Horizontal,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    var firstSetting = db.SystemSettings.OrderBy(s => s.Id).FirstOrDefault();
    if (firstSetting != null)
    {
        var settingsChanged = false;

        if (string.IsNullOrWhiteSpace(firstSetting.LayoutOrientation) || firstSetting.LayoutOrientation == TreeLayoutOrientation.Vertical)
        {
            firstSetting.LayoutOrientation = TreeLayoutOrientation.Horizontal;
            settingsChanged = true;
        }

        if (firstSetting.DefaultZoomPercent < 50 || firstSetting.DefaultZoomPercent == 100 || firstSetting.DefaultZoomPercent == 180)
        {
            firstSetting.DefaultZoomPercent = 70;
            settingsChanged = true;
        }

        if (settingsChanged)
        {
            firstSetting.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Share link route
app.MapControllerRoute(
    name: "share",
    pattern: "share/{token}",
    defaults: new { controller = "Home", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();