using System.ComponentModel.DataAnnotations;

namespace FamilyTree.Models;

public enum UserRole
{
    Admin = 0,
    Viewer = 1
}

public class AppUser
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Viewer;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

