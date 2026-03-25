using System.ComponentModel.DataAnnotations;

namespace FamilyTree.Models;

public class LoginViewModel
{
    [Required]
    [Display(Name = "用户名")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "密码")]
    public string Password { get; set; } = string.Empty;
}

public class CreateUserViewModel
{
    [Required]
    [Display(Name = "用户名")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "密码")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Display(Name = "角色")]
    public UserRole Role { get; set; }
}

