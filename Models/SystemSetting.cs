using System.ComponentModel.DataAnnotations;

namespace FamilyTree.Models;

public static class TreeLayoutOrientation
{
    public const string Vertical = "Vertical";
    public const string Horizontal = "Horizontal";
}

public class SystemSetting
{
    public int Id { get; set; }

    [Required]
    [StringLength(60)]
    public string FamilyName { get; set; } = "周氏家谱";

    [StringLength(40)]
    public string SiteTitle { get; set; } = "族谱管理系统";

    [StringLength(100)]
    public string? ContactName { get; set; }

    [StringLength(30)]
    public string? ContactPhone { get; set; }

    [StringLength(120)]
    public string? ContactEmail { get; set; }

    [StringLength(200)]
    public string? FamilyOrigin { get; set; }

    [StringLength(500)]
    public string? Announcement { get; set; }

    [StringLength(20)]
    public string PrimaryColor { get; set; } = "#dc3545";

    public bool EnableShare { get; set; } = true;

    public bool EnablePublicView { get; set; } = false;

    public bool ShowCardRank { get; set; } = true;

    public bool ShowCardGeneration { get; set; } = true;

    public bool ShowCardGenerationLevel { get; set; } = true;

    public int DefaultZoomPercent { get; set; } = 70;

    [Required]
    [StringLength(20)]
    public string LayoutOrientation { get; set; } = TreeLayoutOrientation.Horizontal;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SystemSettingViewModel
{
    [Required]
    [Display(Name = "家谱名称")]
    public string FamilyName { get; set; } = "周氏家谱";

    [Required]
    [Display(Name = "站点标题")]
    public string SiteTitle { get; set; } = "族谱管理系统";

    [Display(Name = "联系人")]
    public string? ContactName { get; set; }

    [Display(Name = "联系电话")]
    public string? ContactPhone { get; set; }

    [Display(Name = "联系邮箱")]
    public string? ContactEmail { get; set; }

    [Display(Name = "祖籍/祠堂地址")]
    public string? FamilyOrigin { get; set; }

    [Display(Name = "公告信息")]
    public string? Announcement { get; set; }

    [Display(Name = "主题主色")]
    public string PrimaryColor { get; set; } = "#dc3545";

    [Display(Name = "启用分享链接")]
    public bool EnableShare { get; set; } = true;

    [Display(Name = "允许匿名访问")]
    public bool EnablePublicView { get; set; }

    [Display(Name = "卡片显示行几")]
    public bool ShowCardRank { get; set; } = true;

    [Display(Name = "卡片显示辈分")]
    public bool ShowCardGeneration { get; set; } = true;

    [Display(Name = "卡片显示第几代")]
    public bool ShowCardGenerationLevel { get; set; } = true;

    [Display(Name = "默认缩放(%)")]
    [Range(50, 500)]
    public int DefaultZoomPercent { get; set; } = 70;

    [Required]
    [Display(Name = "族谱显示方式")]
    public string LayoutOrientation { get; set; } = TreeLayoutOrientation.Horizontal;
}
