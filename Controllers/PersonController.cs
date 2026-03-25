using FamilyTree.Data;
using FamilyTree.Models;
using FamilyTree.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PersonController : ControllerBase
{
    private readonly FamilyTreeDbContext _context;
    private readonly IWebHostEnvironment _env;

    public PersonController(FamilyTreeDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    /// <summary>
    /// Get all persons as a flat list with DTO
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PersonDto>>> GetAll()
    {
        var persons = await _context.Persons
            .Include(p => p.Children)
            .Include(p => p.Spouse)
            .Include(p => p.Parent)
            .ToListAsync();

        var dtos = persons.Select(p => ToDto(p, persons)).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Get a single person by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PersonDto>> GetById(int id)
    {
        var persons = await _context.Persons
            .Include(p => p.Children)
            .Include(p => p.Spouse)
            .Include(p => p.Parent)
            .ToListAsync();

        var person = persons.FirstOrDefault(p => p.Id == id);
        if (person == null) return NotFound();

        return Ok(ToDto(person, persons));
    }

    /// <summary>
    /// Get tree data - hierarchical structure starting from root
    /// </summary>
    [HttpGet("tree")]
    public async Task<ActionResult> GetTree()
    {
        var persons = await _context.Persons.ToListAsync();
        var root = persons.FirstOrDefault(p => p.IsRoot);
        if (root == null)
        {
            root = persons.FirstOrDefault(p => p.ParentId == null && p.Gender == Gender.Male);
        }
        if (root == null) return Ok(new { });

        var tree = BuildTreeNode(root, persons, new HashSet<int>());
        return Ok(tree);
    }

    /// <summary>
    /// Search persons by name
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<PersonDto>>> Search([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Ok(new List<PersonDto>());

        name = name.Trim();

        var persons = await _context.Persons
            .Include(p => p.Children)
            .Include(p => p.Spouse)
            .Include(p => p.Parent)
            .ToListAsync();

        var results = persons
            .Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(p => ToDto(p, persons))
            .ToList();

        return Ok(results);
    }

    /// <summary>
    /// Create a new person
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost]
    public async Task<ActionResult<PersonDto>> Create([FromBody] PersonCreateDto dto)
    {
        if (dto.ParentId.HasValue && dto.SpouseId.HasValue && dto.ParentId == dto.SpouseId)
            return BadRequest("Parent and spouse cannot be the same person");

        var person = new Person
        {
            Name = dto.Name.Trim(),
            Gender = dto.Gender,
            Generation = dto.Generation?.Trim(),
            Occupation = ResolveOccupation(dto.Name, dto.Occupation),
            BirthDate = dto.BirthDate,
            DeathDate = dto.DeathDate,
            Biography = dto.Biography,
            BirthPlace = dto.BirthPlace,
            CurrentAddress = dto.CurrentAddress,
            Phone = dto.Phone,
            MaleRank = null,
            ParentId = dto.ParentId,
            SpouseId = dto.SpouseId,
            IsRoot = dto.IsRoot,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Persons.Add(person);
        await _context.SaveChangesAsync();

        // If spouse is set, update the spouse's SpouseId too
        if (dto.SpouseId.HasValue)
        {
            var spouse = await _context.Persons.FindAsync(dto.SpouseId.Value);
            if (spouse != null && spouse.SpouseId == null)
            {
                spouse.SpouseId = person.Id;
                await _context.SaveChangesAsync();
            }
        }

        var persons = await _context.Persons
            .Include(p => p.Children)
            .Include(p => p.Spouse)
            .Include(p => p.Parent)
            .ToListAsync();

        return CreatedAtAction(nameof(GetById), new { id = person.Id }, ToDto(person, persons));
    }

    /// <summary>
    /// Update an existing person
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPut("{id}")]
    public async Task<ActionResult<PersonDto>> Update(int id, [FromBody] PersonCreateDto dto)
    {
        var person = await _context.Persons.FindAsync(id);
        if (person == null) return NotFound();

        if (dto.ParentId == id)
            return BadRequest("A person cannot be their own parent");

        if (dto.SpouseId == id)
            return BadRequest("A person cannot be their own spouse");

        if (dto.ParentId.HasValue && await WouldCreateCycle(id, dto.ParentId.Value))
            return BadRequest("Invalid parent relation: cycle detected");

        if (dto.ParentId.HasValue && dto.SpouseId.HasValue && dto.ParentId == dto.SpouseId)
            return BadRequest("Parent and spouse cannot be the same person");

        var oldSpouseId = person.SpouseId;

        person.Name = dto.Name.Trim();
        person.Gender = dto.Gender;
        person.Generation = dto.Generation?.Trim();
        person.Occupation = ResolveOccupation(dto.Name, dto.Occupation);
        person.BirthDate = dto.BirthDate;
        person.DeathDate = dto.DeathDate;
        person.Biography = dto.Biography;
        person.BirthPlace = dto.BirthPlace;
        person.CurrentAddress = dto.CurrentAddress;
        person.Phone = dto.Phone;
        person.MaleRank = null;
        person.ParentId = dto.ParentId;
        person.SpouseId = dto.SpouseId;
        person.IsRoot = dto.IsRoot;
        person.UpdatedAt = DateTime.UtcNow;

        // Handle spouse relationship changes
        if (oldSpouseId != dto.SpouseId)
        {
            // Remove old spouse link
            if (oldSpouseId.HasValue)
            {
                var oldSpouse = await _context.Persons.FindAsync(oldSpouseId.Value);
                if (oldSpouse != null && oldSpouse.SpouseId == id)
                {
                    oldSpouse.SpouseId = null;
                }
            }
            // Set new spouse link
            if (dto.SpouseId.HasValue)
            {
                var newSpouse = await _context.Persons.FindAsync(dto.SpouseId.Value);
                if (newSpouse != null)
                {
                    newSpouse.SpouseId = id;
                }
            }
        }

        await _context.SaveChangesAsync();

        var persons = await _context.Persons
            .Include(p => p.Children)
            .Include(p => p.Spouse)
            .Include(p => p.Parent)
            .ToListAsync();

        return Ok(ToDto(person, persons));
    }

    /// <summary>
    /// Delete a person
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var person = await _context.Persons.FindAsync(id);
        if (person == null) return NotFound();

        // Remove spouse reference
        if (person.SpouseId.HasValue)
        {
            var spouse = await _context.Persons.FindAsync(person.SpouseId.Value);
            if (spouse != null && spouse.SpouseId == id)
            {
                spouse.SpouseId = null;
            }
        }

        // Set children's parent to null
        var children = await _context.Persons.Where(p => p.ParentId == id).ToListAsync();
        foreach (var child in children)
        {
            child.ParentId = null;
        }

        _context.Persons.Remove(person);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Upload a photo for a person
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("{id}/photo")]
    public async Task<ActionResult> UploadPhoto(int id, IFormFile file)
    {
        var person = await _context.Persons.FindAsync(id);
        if (person == null) return NotFound();

        if (file.Length == 0)
            return BadRequest("No file uploaded");

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        if (!Directory.Exists(uploadsDir))
            Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{id}_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        person.PhotoUrl = $"/uploads/{fileName}";
        person.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { photoUrl = person.PhotoUrl });
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpGet("export-xmind")]
    public async Task<ActionResult> ExportXmind()
    {
        var persons = await _context.Persons.AsNoTracking().ToListAsync();
        if (!persons.Any())
            return BadRequest("当前没有可导出的成员数据");

        var familyName = await _context.SystemSettings
            .OrderBy(s => s.Id)
            .Select(s => s.FamilyName)
            .FirstOrDefaultAsync() ?? "族谱";

        var bytes = XmindService.Export(persons, familyName);
        var fileName = $"{SanitizeFileName(familyName)}-{DateTime.Now:yyyyMMddHHmmss}.xmind";
        return File(bytes, "application/vnd.xmind.workbook", fileName);
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("import-xmind")]
    public async Task<ActionResult> ImportXmind(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("请选择要导入的 XMind 文件");

        if (!string.Equals(Path.GetExtension(file.FileName), ".xmind", StringComparison.OrdinalIgnoreCase))
            return BadRequest("仅支持导入 .xmind 文件");

        XmindImportResult importResult;
        try
        {
            await using var readStream = file.OpenReadStream();
            importResult = XmindService.Parse(readStream);
        }
        catch (Exception ex)
        {
            return BadRequest($"XMind 解析失败：{ex.Message}");
        }

        if (!importResult.Persons.Any())
            return BadRequest("未从 XMind 文件中解析到成员数据");

        var now = DateTime.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM ShareLinks;");
            await _context.Database.ExecuteSqlRawAsync("UPDATE Persons SET SpouseId = NULL, ParentId = NULL;");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Persons;");
            try
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('Persons','ShareLinks');");
            }
            catch
            {
                // sqlite_sequence may not exist in some environments.
            }

            _context.ChangeTracker.Clear();

            var tempToDbId = new Dictionary<int, int>();
            foreach (var imported in importResult.Persons)
            {
                var entity = new Person
                {
                    Name = imported.Name.Trim(),
                    Gender = imported.Gender,
                    Generation = imported.Generation?.Trim(),
                    Occupation = imported.Occupation?.Trim(),
                    BirthDate = imported.BirthDate,
                    ParentId = imported.ParentTempId.HasValue && tempToDbId.TryGetValue(imported.ParentTempId.Value, out var parentId)
                        ? parentId
                        : null,
                    IsRoot = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.Persons.Add(entity);
                await _context.SaveChangesAsync();
                tempToDbId[imported.TempId] = entity.Id;
            }

            var allPersons = await _context.Persons.OrderBy(p => p.Id).ToListAsync();
            foreach (var imported in importResult.Persons.Where(p => !string.IsNullOrWhiteSpace(p.SpouseName)))
            {
                if (!tempToDbId.TryGetValue(imported.TempId, out var personId))
                    continue;

                var person = allPersons.First(p => p.Id == personId);
                if (person.SpouseId.HasValue)
                    continue;

                var spouse = allPersons.FirstOrDefault(p =>
                    p.Id != person.Id &&
                    string.Equals(p.Name, imported.SpouseName, StringComparison.OrdinalIgnoreCase));

                if (spouse != null && spouse.SpouseId.HasValue && spouse.SpouseId != person.Id)
                {
                    spouse = null;
                }

                if (spouse == null)
                {
                    spouse = new Person
                    {
                        Name = imported.SpouseName!.Trim(),
                        Gender = imported.SpouseGender ?? (person.Gender == Gender.Male ? Gender.Female : Gender.Male),
                        Generation = imported.Generation?.Trim(),
                        ParentId = null,
                        IsRoot = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    _context.Persons.Add(spouse);
                    await _context.SaveChangesAsync();
                    allPersons.Add(spouse);
                }

                person.SpouseId = spouse.Id;
                spouse.SpouseId = person.Id;
                person.UpdatedAt = now;
                spouse.UpdatedAt = now;
            }

            foreach (var item in allPersons)
            {
                item.IsRoot = false;
            }

            if (tempToDbId.TryGetValue(importResult.Persons[0].TempId, out var rootId))
            {
                var rootPerson = allPersons.FirstOrDefault(p => p.Id == rootId);
                if (rootPerson != null)
                {
                    rootPerson.IsRoot = true;
                    rootPerson.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync();
            await _context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
            await transaction.CommitAsync();

            return Ok(new
            {
                importedCount = allPersons.Count,
                rootName = allPersons.FirstOrDefault(p => p.IsRoot)?.Name,
                sheetTitle = importResult.SheetTitle
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            try
            {
                await _context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
            }
            catch
            {
                // ignore cleanup failures after rollback
            }

            return BadRequest($"XMind 导入失败：{ex.Message}");
        }
    }

    /// <summary>
    /// Generate share link
    /// </summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("share")]
    public async Task<ActionResult> CreateShareLink()
    {
        var link = new ShareLink
        {
            Token = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _context.ShareLinks.Add(link);
        await _context.SaveChangesAsync();

        var url = $"{Request.Scheme}://{Request.Host}/share/{link.Token}";
        return Ok(new { url, token = link.Token, expiresAt = link.ExpiresAt });
    }

    /// <summary>
    /// Validate share token
    /// </summary>
    [HttpGet("share/{token}")]
    public async Task<ActionResult> ValidateShareLink(string token)
    {
        var link = await _context.ShareLinks
            .FirstOrDefaultAsync(l => l.Token == token);

        if (link == null) return NotFound();
        if (link.ExpiresAt.HasValue && link.ExpiresAt < DateTime.UtcNow)
            return BadRequest("Link expired");

        return Ok(new { valid = true });
    }

    /// <summary>
    /// Get statistics
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpGet("stats")]
    public async Task<ActionResult> GetStats()
    {
        var persons = await _context.Persons.ToListAsync();
        var totalMembers = persons.Count;
        var maleCount = persons.Count(p => p.Gender == Gender.Male);
        var femaleCount = persons.Count(p => p.Gender == Gender.Female);
        var generations = persons
            .Where(p => !string.IsNullOrEmpty(p.Generation))
            .Select(p => p.Generation)
            .Distinct()
            .ToList();

        return Ok(new
        {
            totalMembers,
            maleCount,
            femaleCount,
            generationCount = generations.Count,
            generations
        });
    }

    [HttpGet("permissions")]
    public ActionResult GetPermissions()
    {
        var role = User.IsInRole(nameof(UserRole.Admin)) ? nameof(UserRole.Admin) : nameof(UserRole.Viewer);
        return Ok(new
        {
            role,
            canManageMembers = role == nameof(UserRole.Admin)
        });
    }

    [HttpGet("site-settings")]
    public async Task<ActionResult> GetSiteSettings()
    {
        var setting = await _context.SystemSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (setting == null)
        {
            return Ok(new
            {
                siteTitle = "族谱管理系统",
                familyName = "周氏家谱",
                primaryColor = "#dc3545",
                defaultZoomPercent = 70,
                enableShare = true,
                layoutOrientation = TreeLayoutOrientation.Horizontal,
                showCardRank = true,
                showCardGeneration = true,
                showCardGenerationLevel = true
            });
        }

        return Ok(new
        {
            siteTitle = setting.SiteTitle,
            familyName = setting.FamilyName,
            primaryColor = setting.PrimaryColor,
            defaultZoomPercent = setting.DefaultZoomPercent,
            enableShare = setting.EnableShare,
            showCardRank = setting.ShowCardRank,
            showCardGeneration = setting.ShowCardGeneration,
            showCardGenerationLevel = setting.ShowCardGenerationLevel,
            layoutOrientation = string.IsNullOrWhiteSpace(setting.LayoutOrientation)
                ? TreeLayoutOrientation.Horizontal
                : setting.LayoutOrientation
        });
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "family-tree" : sanitized;
    }

    private PersonDto ToDto(Person p, List<Person> allPersons)
    {
        // Count only direct children (non-spouse)
        var directChildren = allPersons.Where(c => c.ParentId == p.Id).ToList();

        return new PersonDto
        {
            Id = p.Id,
            Name = p.Name,
            Gender = p.Gender,
            Generation = p.Generation,
            Occupation = p.Occupation,
            BirthDate = p.BirthDate,
            DeathDate = p.DeathDate,
            Biography = p.Biography,
            PhotoUrl = p.PhotoUrl,
            BirthPlace = p.BirthPlace,
            CurrentAddress = p.CurrentAddress,
            Phone = p.Phone,
            ParentId = p.ParentId,
            SpouseId = p.SpouseId,
            SpouseName = p.SpouseId.HasValue ? allPersons.FirstOrDefault(s => s.Id == p.SpouseId)?.Name : null,
            ParentName = p.ParentId.HasValue ? allPersons.FirstOrDefault(s => s.Id == p.ParentId)?.Name : null,
            IsRoot = p.IsRoot,
            SonsCount = directChildren.Count(c => c.Gender == Gender.Male),
            DaughtersCount = directChildren.Count(c => c.Gender == Gender.Female),
            Rank = GetRank(p, allPersons)
        };
    }

    private object BuildTreeNode(Person person, List<Person> allPersons, HashSet<int> visited, int depth = 0)
    {
        if (!visited.Add(person.Id) || depth > 25)
        {
            return new
            {
                id = person.Id,
                name = person.Name,
                gender = person.Gender,
                generation = person.Generation,
                occupation = person.Occupation,
                birthDate = person.BirthDate,
                deathDate = person.DeathDate,
                biography = person.Biography,
                photoUrl = person.PhotoUrl,
                birthPlace = person.BirthPlace,
                phone = person.Phone,
                isRoot = person.IsRoot,
                sonsCount = 0,
                daughtersCount = 0,
                rank = GetRank(person, allPersons),
                spouse = (object?)null,
                children = new List<object>()
            };
        }

        var spouse = person.SpouseId.HasValue
            ? allPersons.FirstOrDefault(p => p.Id == person.SpouseId)
            : allPersons.FirstOrDefault(p => p.SpouseId == person.Id);

        // Get children - children of this person (by ParentId)
        var children = allPersons
            .Where(p => p.ParentId == person.Id && (spouse == null || p.Id != spouse.Id))
            .OrderBy(p => p.BirthDate)
            .ToList();

        // Also include children of the spouse that have ParentId = person.Id
        if (spouse != null)
        {
            var spouseChildren = allPersons
                .Where(p => p.ParentId == spouse.Id && p.Id != person.Id && !children.Any(c => c.Id == p.Id))
                .OrderBy(p => p.BirthDate)
                .ToList();
            children.AddRange(spouseChildren);
            children = children.OrderBy(p => p.BirthDate).ToList();
        }

        // Filter out children who are spouses (have a SpouseId pointing to someone in same parent)
        // Keep male children and unmarried female children
        var primaryChildren = new List<Person>();
        var processedSpouseIds = new HashSet<int>();

        foreach (var child in children)
        {
            if (processedSpouseIds.Contains(child.Id)) continue;

            primaryChildren.Add(child);

            if (child.SpouseId.HasValue)
            {
                processedSpouseIds.Add(child.SpouseId.Value);
            }
        }

        var directChildren = allPersons.Where(c => c.ParentId == person.Id).ToList();

        return new
        {
            id = person.Id,
            name = person.Name,
            gender = person.Gender,
            generation = person.Generation,
            occupation = person.Occupation,
            birthDate = person.BirthDate,
            deathDate = person.DeathDate,
            biography = person.Biography,
            photoUrl = person.PhotoUrl,
            birthPlace = person.BirthPlace,
            phone = person.Phone,
            isRoot = person.IsRoot,
            rank = GetRank(person, allPersons),
            sonsCount = directChildren.Count(c => c.Gender == Gender.Male),
            daughtersCount = directChildren.Count(c => c.Gender == Gender.Female),
            spouse = spouse != null ? new
            {
                id = spouse.Id,
                name = spouse.Name,
                gender = spouse.Gender,
                generation = spouse.Generation,
                occupation = spouse.Occupation,
                birthDate = spouse.BirthDate,
                deathDate = spouse.DeathDate,
                biography = spouse.Biography,
                photoUrl = spouse.PhotoUrl,
                birthPlace = spouse.BirthPlace,
                phone = spouse.Phone
            } : null,
            children = primaryChildren.Select(c => BuildTreeNode(c, allPersons, visited, depth + 1)).ToList()
        };
    }

    private int? GetRank(Person person, List<Person> allPersons)
    {
        if (person.Gender != Gender.Male)
            return null;

        var generationKey = (person.Generation ?? string.Empty).Trim();
        var malePeers = allPersons
            .Where(p => p.Gender == Gender.Male && ((p.Generation ?? string.Empty).Trim() == generationKey))
            .OrderBy(p => p.BirthDate ?? DateTime.MaxValue)
            .ThenBy(p => p.Id)
            .ToList();

        var index = malePeers.FindIndex(p => p.Id == person.Id);
        return index >= 0 ? index + 1 : 1;
    }

    private static string? ResolveOccupation(string? name, string? occupation)
    {
        if (!string.IsNullOrWhiteSpace(name) && name.Contains("农民", StringComparison.Ordinal))
            return "农民";

        var normalized = occupation?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task<bool> WouldCreateCycle(int personId, int newParentId)
    {
        var current = newParentId;
        var guard = new HashSet<int>();

        while (true)
        {
            if (current == personId)
                return true;

            if (!guard.Add(current))
                return true;

            var parentCandidateId = current;
            var next = await _context.Persons
                .Where(p => p.Id == parentCandidateId)
                .Select(p => p.ParentId)
                .FirstOrDefaultAsync();

            if (!next.HasValue)
                return false;

            current = next.Value;
        }
    }

}

public class PersonCreateDto
{
    public string Name { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public string? Generation { get; set; }
    public string? Occupation { get; set; }
    public DateTime? BirthDate { get; set; }
    public DateTime? DeathDate { get; set; }
    public string? Biography { get; set; }
    public string? BirthPlace { get; set; }
    public string? CurrentAddress { get; set; }
    public string? Phone { get; set; }
    public int? ParentId { get; set; }
    public int? SpouseId { get; set; }
    public bool IsRoot { get; set; }
}

