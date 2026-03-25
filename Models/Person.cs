using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace FamilyTree.Models;

public enum Gender
{
    Male = 0,
    Female = 1
}

public class Person
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public Gender Gender { get; set; }

    [StringLength(20)]
    public string? Generation { get; set; } // 辈分，如 "京"、"永"、"学"、"大"

    [StringLength(50)]
    public string? Occupation { get; set; }

    public DateTime? BirthDate { get; set; }

    public DateTime? DeathDate { get; set; }

    [StringLength(500)]
    public string? Biography { get; set; } // 个人简介

    [StringLength(200)]
    public string? PhotoUrl { get; set; }

    [StringLength(100)]
    public string? BirthPlace { get; set; } // 出生地

    [StringLength(100)]
    public string? CurrentAddress { get; set; } // 现居地

    [StringLength(20)]
    public string? Phone { get; set; }

    // 仅男性使用：可手工设置同父兄弟中的排行序号
    public int? MaleRank { get; set; }

    // Self-referencing relationship: parent
    public int? ParentId { get; set; }

    [ForeignKey("ParentId")]
    [JsonIgnore]
    public Person? Parent { get; set; }

    // Spouse relationship
    public int? SpouseId { get; set; }

    [ForeignKey("SpouseId")]
    [JsonIgnore]
    public Person? Spouse { get; set; }

    // Children collection
    [JsonIgnore]
    public ICollection<Person> Children { get; set; } = new List<Person>();

    // 是否为户主/族长
    public bool IsRoot { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for API responses to avoid circular references
/// </summary>
public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public string? Generation { get; set; }
    public string? Occupation { get; set; }
    public DateTime? BirthDate { get; set; }
    public DateTime? DeathDate { get; set; }
    public string? Biography { get; set; }
    public string? PhotoUrl { get; set; }
    public string? BirthPlace { get; set; }
    public string? CurrentAddress { get; set; }
    public string? Phone { get; set; }
    public int? ParentId { get; set; }
    public int? SpouseId { get; set; }
    public string? SpouseName { get; set; }
    public string? ParentName { get; set; }
    public bool IsRoot { get; set; }
    public int SonsCount { get; set; }
    public int DaughtersCount { get; set; }
    public int? Rank { get; set; }
    public List<PersonDto> Children { get; set; } = new();
}

public class ShareLink
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }
}

