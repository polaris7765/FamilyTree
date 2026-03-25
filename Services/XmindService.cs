using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FamilyTree.Models;

namespace FamilyTree.Services;

public sealed class XmindImportPerson
{
    public int TempId { get; set; }
    public int? ParentTempId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public string? Generation { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Occupation { get; set; }
    public bool IsRoot { get; set; }
    public string? SpouseName { get; set; }
    public Gender? SpouseGender { get; set; }
}

public sealed class XmindImportResult
{
    public string? SheetTitle { get; set; }
    public List<XmindImportPerson> Persons { get; } = new();
}

public static class XmindService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Regex BirthDateRegex = new(@"((?:19|20)\d{2})[\.\-/年](\d{1,2})[\.\-/月](\d{1,2})", RegexOptions.Compiled);
    private static readonly Regex StructuredFieldRegex = new(@"^(?<key>[^:：]+)\s*[:：]\s*(?<value>.+)$", RegexOptions.Compiled);
    private static readonly Regex SpouseRegex = new(@"(?:配偶|妻|夫)\s*[:：]\s*([^｜|,，；;\s]+)", RegexOptions.Compiled);
    private static readonly Regex GenerationRegex = new(@"(?:辈分|辈)\s*[:：]\s*([^｜|,，；;\s]+)", RegexOptions.Compiled);
    private static readonly Regex OccupationRegex = new(@"职业\s*[:：]\s*([^｜|,，；;]+)", RegexOptions.Compiled);
    private static readonly Regex ChineseNameRegex = new(@"([\u4e00-\u9fa5]{2,10})", RegexOptions.Compiled);

    public static XmindImportResult Parse(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: Encoding.UTF8);
        var contentEntry = archive.GetEntry("content.json") ?? throw new InvalidOperationException("XMind 文件中缺少 content.json");

        using var reader = new StreamReader(contentEntry.Open(), Encoding.UTF8);
        var json = reader.ReadToEnd();
        var rootArray = JsonNode.Parse(json)?.AsArray() ?? throw new InvalidOperationException("XMind content.json 格式无效");
        var sheet = rootArray.OfType<JsonObject>().FirstOrDefault() ?? throw new InvalidOperationException("XMind 中未找到工作表");
        var rootTopic = sheet["rootTopic"]?.AsObject() ?? throw new InvalidOperationException("XMind 中未找到根主题");

        var result = new XmindImportResult
        {
            SheetTitle = sheet["title"]?.GetValue<string>() ?? rootTopic["title"]?.GetValue<string>()
        };

        var counter = 0;
        WalkTopic(rootTopic, depth: 0, parentTempId: null, result, ref counter);

        if (result.Persons.Count > 0)
        {
            result.Persons[0].IsRoot = true;
        }

        return result;
    }

    public static byte[] Export(IReadOnlyList<Person> persons, string sheetTitle)
    {
        if (persons.Count == 0)
            throw new InvalidOperationException("当前没有可导出的成员数据");

        var rootPerson = persons.FirstOrDefault(p => p.IsRoot)
                         ?? persons.FirstOrDefault(p => p.ParentId == null && p.Gender == Gender.Male)
                         ?? persons.FirstOrDefault(p => p.ParentId == null)
                         ?? persons[0];

        var visited = new HashSet<int>();
        var attached = new JsonArray(BuildPersonTopic(rootPerson, persons, visited));

        var workbookRoot = new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString("N"),
            ["title"] = sheetTitle,
            ["structureClass"] = "org.xmind.ui.logic.right",
            ["children"] = new JsonObject
            {
                ["attached"] = attached
            }
        };

        var content = new JsonArray
        {
            new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["title"] = sheetTitle,
                ["rootTopic"] = workbookRoot
            }
        };

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: Encoding.UTF8))
        {
            WriteArchiveEntry(archive, "content.json", JsonSerializer.Serialize(content, JsonOptions));
            WriteArchiveEntry(archive, "metadata.json", JsonSerializer.Serialize(new
            {
                creator = new { name = "FamilyTree" },
                activeSheetId = content[0]?["id"]?.GetValue<string>(),
                created = DateTimeOffset.UtcNow.ToString("O")
            }, JsonOptions));
            WriteArchiveEntry(archive, "manifest.json", JsonSerializer.Serialize(new
            {
                fileEntries = new Dictionary<string, object?>
                {
                    ["content.json"] = new { },
                    ["metadata.json"] = new { }
                }
            }, JsonOptions));
        }

        return stream.ToArray();
    }

    private static void WalkTopic(JsonObject topic, int depth, int? parentTempId, XmindImportResult result, ref int counter)
    {
        var title = Normalize(topic["title"]?.GetValue<string>());
        var parsed = ParseTopicTitle(title, depth, isTopLevel: parentTempId == null && result.Persons.Count == 0);

        var nextParentId = parentTempId;
        if (parsed != null)
        {
            parsed.TempId = ++counter;
            parsed.ParentTempId = parentTempId;
            result.Persons.Add(parsed);
            nextParentId = parsed.TempId;
        }

        var attached = topic["children"]?["attached"]?.AsArray();
        if (attached == null) return;

        foreach (var childNode in attached.OfType<JsonObject>())
        {
            WalkTopic(childNode, depth + 1, nextParentId, result, ref counter);
        }
    }

    private static XmindImportPerson? ParseTopicTitle(string? title, int depth, bool isTopLevel)
    {
        var normalized = Normalize(title);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (depth == 0 && normalized.Contains("家谱", StringComparison.Ordinal) && !normalized.Contains('｜') && !normalized.Contains('|'))
            return null;

        string? name = null;
        string? generation = null;
        DateTime? birthDate = null;
        string? occupation = null;
        string? spouseName = null;
        Gender? spouseGender = null;
        var gender = Gender.Male;
        var hasStructuredField = false;

        var parts = normalized.Split(['｜', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 0)
        {
            name = ExtractPossibleName(parts[0]);
        }

        foreach (var part in parts)
        {
            var match = StructuredFieldRegex.Match(part);
            if (!match.Success) continue;

            hasStructuredField = true;
            var key = match.Groups["key"].Value.Trim();
            var value = match.Groups["value"].Value.Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;

            switch (key)
            {
                case "姓名":
                case "名字":
                    name = ExtractPossibleName(value) ?? name;
                    break;
                case "性别":
                    gender = ParseGender(value) ?? gender;
                    break;
                case "辈分":
                case "辈":
                    generation = value;
                    break;
                case "出生":
                case "出生日期":
                    birthDate = ParseBirthDate(value) ?? birthDate;
                    break;
                case "职业":
                    occupation = value;
                    break;
                case "配偶":
                case "妻":
                    spouseName = value;
                    spouseGender = Gender.Female;
                    break;
                case "夫":
                    spouseName = value;
                    spouseGender = Gender.Male;
                    break;
            }
        }

        if (!hasStructuredField)
        {
            name ??= ExtractPossibleName(normalized);
            var generationMatch = GenerationRegex.Match(normalized);
            if (string.IsNullOrWhiteSpace(generation) && generationMatch.Success)
                generation = generationMatch.Groups[1].Value;

            var occupationMatch = OccupationRegex.Match(normalized);
            if (string.IsNullOrWhiteSpace(occupation) && occupationMatch.Success)
                occupation = occupationMatch.Groups[1].Value;
            birthDate ??= ParseBirthDate(normalized);

            var spouseMatch = SpouseRegex.Match(normalized);
            if (spouseMatch.Success)
            {
                spouseName = spouseMatch.Groups[1].Value.Trim();
                spouseGender = normalized.Contains("夫", StringComparison.Ordinal) ? Gender.Male : Gender.Female;
            }

            gender = ParseGender(normalized) ?? gender;
        }

        if (string.IsNullOrWhiteSpace(name) || !IsValidName(name))
            return null;

        occupation = ResolveOccupation(name, occupation);
        spouseName = string.IsNullOrWhiteSpace(spouseName) || !IsValidName(spouseName) ? null : spouseName;
        generation = string.IsNullOrWhiteSpace(generation) ? null : generation.Trim();

        return new XmindImportPerson
        {
            Name = name.Trim(),
            Gender = gender,
            Generation = generation,
            BirthDate = birthDate,
            Occupation = occupation,
            IsRoot = isTopLevel,
            SpouseName = spouseName,
            SpouseGender = spouseGender ?? (gender == Gender.Male ? Gender.Female : Gender.Male)
        };
    }

    private static JsonObject BuildPersonTopic(Person person, IReadOnlyList<Person> persons, HashSet<int> visited, int depth = 0)
    {
        if (!visited.Add(person.Id) || depth > 30)
        {
            return new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["title"] = FormatTitle(person, FindSpouse(person, persons))
            };
        }

        var spouse = FindSpouse(person, persons);
        var children = GetPrimaryChildren(person, spouse, persons);
        var attachedChildren = new JsonArray();

        foreach (var child in children)
        {
            attachedChildren.Add(BuildPersonTopic(child, persons, visited, depth + 1));
        }

        var topic = new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString("N"),
            ["title"] = FormatTitle(person, spouse)
        };

        if (attachedChildren.Count > 0)
        {
            topic["children"] = new JsonObject
            {
                ["attached"] = attachedChildren
            };
        }

        return topic;
    }

    private static Person? FindSpouse(Person person, IReadOnlyList<Person> persons)
    {
        return person.SpouseId.HasValue
            ? persons.FirstOrDefault(p => p.Id == person.SpouseId.Value)
            : persons.FirstOrDefault(p => p.SpouseId == person.Id);
    }

    private static List<Person> GetPrimaryChildren(Person person, Person? spouse, IReadOnlyList<Person> persons)
    {
        var children = persons
            .Where(p => p.ParentId == person.Id && (spouse == null || p.Id != spouse.Id))
            .OrderBy(p => p.BirthDate ?? DateTime.MaxValue)
            .ThenBy(p => p.Id)
            .ToList();

        if (spouse != null)
        {
            var spouseChildren = persons
                .Where(p => p.ParentId == spouse.Id && p.Id != person.Id && children.All(c => c.Id != p.Id))
                .OrderBy(p => p.BirthDate ?? DateTime.MaxValue)
                .ThenBy(p => p.Id)
                .ToList();
            children.AddRange(spouseChildren);
            children = children.OrderBy(p => p.BirthDate ?? DateTime.MaxValue).ThenBy(p => p.Id).ToList();
        }

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

        return primaryChildren;
    }

    private static string FormatTitle(Person person, Person? spouse)
    {
        var parts = new List<string>
        {
            person.Name,
            $"性别:{(person.Gender == Gender.Female ? "女" : "男")}"
        };

        if (!string.IsNullOrWhiteSpace(person.Generation))
            parts.Add($"辈分:{person.Generation!.Trim()}");
        if (person.BirthDate.HasValue)
            parts.Add($"出生:{person.BirthDate:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(person.Occupation))
            parts.Add($"职业:{person.Occupation!.Trim()}");
        if (spouse != null)
            parts.Add($"配偶:{spouse.Name}");

        return string.Join('｜', parts);
    }

    private static void WriteArchiveEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static string Normalize(string? text)
        => string.Join(' ', (text ?? string.Empty).Replace("\u200b", string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? ExtractPossibleName(string? raw)
    {
        var normalized = Normalize(raw)
            .Replace("姓名:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("姓名：", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (normalized.Contains("家谱", StringComparison.Ordinal))
            return null;

        var match = ChineseNameRegex.Match(normalized);
        if (match.Success)
            return match.Groups[1].Value;

        var firstToken = normalized.Split([':', '：', '｜', '|', ' ', ',', '，'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstToken) ? null : firstToken.Trim();
    }

    private static bool IsValidName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        if (normalized.Length is < 2 or > 20) return false;

        var badWords = new[] { "家谱", "未连接", "说明", "统计", "欢迎使用", "rootTopic" };
        return badWords.All(bad => !normalized.Contains(bad, StringComparison.OrdinalIgnoreCase));
    }

    private static Gender? ParseGender(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Contains("女", StringComparison.Ordinal) || value.Contains("Female", StringComparison.OrdinalIgnoreCase)) return Gender.Female;
        if (value.Contains("男", StringComparison.Ordinal) || value.Contains("Male", StringComparison.OrdinalIgnoreCase)) return Gender.Male;
        return null;
    }

    private static DateTime? ParseBirthDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var match = BirthDateRegex.Match(value);
        if (!match.Success) return null;
        if (!int.TryParse(match.Groups[1].Value, out var year) ||
            !int.TryParse(match.Groups[2].Value, out var month) ||
            !int.TryParse(match.Groups[3].Value, out var day))
        {
            return null;
        }

        try
        {
            return new DateTime(year, month, day);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveOccupation(string? name, string? occupation)
    {
        if (!string.IsNullOrWhiteSpace(name) && name.Contains("农民", StringComparison.Ordinal))
            return "农民";
        if (!string.IsNullOrWhiteSpace(occupation) && occupation.Contains("农民", StringComparison.Ordinal))
            return "农民";
        return string.IsNullOrWhiteSpace(occupation) ? null : occupation.Trim();
    }
}


