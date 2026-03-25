using FamilyTree.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyTree.Data;

public class FamilyTreeDbContext : DbContext
{
    public FamilyTreeDbContext(DbContextOptions<FamilyTreeDbContext> options) : base(options)
    {
    }

    public DbSet<Person> Persons => Set<Person>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Person>(entity =>
        {
            // Self-referencing parent-child relationship
            entity.HasOne(p => p.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Spouse relationship (one-to-one, but allowing null)
            entity.HasOne(p => p.Spouse)
                .WithOne()
                .HasForeignKey<Person>(p => p.SpouseId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => p.Name);
            entity.HasIndex(p => p.ParentId);
            entity.Property(p => p.Occupation).HasMaxLength(50);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.Property(s => s.FamilyName).HasMaxLength(60).IsRequired();
            entity.Property(s => s.SiteTitle).HasMaxLength(40).IsRequired();
            entity.Property(s => s.PrimaryColor).HasMaxLength(20).IsRequired();
            entity.Property(s => s.LayoutOrientation).HasMaxLength(20).IsRequired();
            entity.Property(s => s.ShowCardRank).HasDefaultValue(true);
            entity.Property(s => s.ShowCardGeneration).HasDefaultValue(true);
            entity.Property(s => s.ShowCardGenerationLevel).HasDefaultValue(true);
        });

        // Seed sample data - a Chinese family tree
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // 京字辈 - 始祖
        var root = new Person
        {
            Id = 1, Name = "冯象民", Gender = Gender.Male, Generation = "京",
            IsRoot = true, SpouseId = 2,
            BirthDate = new DateTime(1920, 3, 15),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var rootSpouse = new Person
        {
            Id = 2, Name = "陈氏", Gender = Gender.Female, Generation = "京",
            BirthDate = new DateTime(1922, 7, 20),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 永字辈 - 第二代
        var child1 = new Person
        {
            Id = 3, Name = "冯永祥", Gender = Gender.Male, Generation = "永",
            ParentId = 1, SpouseId = 4,
            BirthDate = new DateTime(1945, 5, 10),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var child1Spouse = new Person
        {
            Id = 4, Name = "王氏", Gender = Gender.Female, Generation = "永",
            ParentId = 1,
            BirthDate = new DateTime(1947, 9, 8),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        var child2 = new Person
        {
            Id = 5, Name = "冯永贤", Gender = Gender.Male, Generation = "永",
            ParentId = 1, SpouseId = 6,
            BirthDate = new DateTime(1948, 11, 22),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var child2Spouse = new Person
        {
            Id = 6, Name = "甘氏", Gender = Gender.Female, Generation = "永",
            ParentId = 1,
            BirthDate = new DateTime(1950, 2, 14),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        var child3 = new Person
        {
            Id = 7, Name = "冯永泉", Gender = Gender.Male, Generation = "永",
            ParentId = 1, SpouseId = 8,
            BirthDate = new DateTime(1952, 8, 3),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var child3Spouse = new Person
        {
            Id = 8, Name = "李氏", Gender = Gender.Female, Generation = "永",
            ParentId = 1,
            BirthDate = new DateTime(1954, 4, 17),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 学字辈 - 第三代（冯永祥的子女）
        var gc1 = new Person
        {
            Id = 9, Name = "冯学滨", Gender = Gender.Male, Generation = "学",
            ParentId = 3, SpouseId = 10,
            BirthDate = new DateTime(1970, 1, 25),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var gc1Spouse = new Person
        {
            Id = 10, Name = "夏氏", Gender = Gender.Female, Generation = "学",
            ParentId = 3,
            BirthDate = new DateTime(1972, 6, 30),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        var gc2 = new Person
        {
            Id = 11, Name = "冯学梅", Gender = Gender.Female, Generation = "学",
            ParentId = 3,
            BirthDate = new DateTime(1973, 12, 5),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 学字辈 - 第三代（冯永贤的子女）
        var gc3 = new Person
        {
            Id = 12, Name = "冯学魁", Gender = Gender.Male, Generation = "学",
            ParentId = 5, SpouseId = 13,
            BirthDate = new DateTime(1972, 3, 18),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var gc3Spouse = new Person
        {
            Id = 13, Name = "刘氏", Gender = Gender.Female, Generation = "学",
            ParentId = 5,
            BirthDate = new DateTime(1974, 8, 22),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        var gc4 = new Person
        {
            Id = 14, Name = "冯学慧", Gender = Gender.Female, Generation = "学",
            ParentId = 5,
            BirthDate = new DateTime(1975, 7, 11),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 学字辈 - 第三代（冯永泉的子女）
        var gc5 = new Person
        {
            Id = 15, Name = "冯学荣", Gender = Gender.Male, Generation = "学",
            ParentId = 7, SpouseId = 16,
            BirthDate = new DateTime(1976, 9, 28),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var gc5Spouse = new Person
        {
            Id = 16, Name = "李秀华", Gender = Gender.Female, Generation = "学",
            ParentId = 7,
            BirthDate = new DateTime(1978, 3, 14),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        var gc6 = new Person
        {
            Id = 17, Name = "冯学生", Gender = Gender.Male, Generation = "学",
            ParentId = 7,
            BirthDate = new DateTime(1980, 11, 7),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 大字辈 - 第四代（冯学滨的子女）
        var ggc1 = new Person
        {
            Id = 18, Name = "冯大辉", Gender = Gender.Male, Generation = "大",
            ParentId = 9,
            BirthDate = new DateTime(1995, 4, 12),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var ggc2 = new Person
        {
            Id = 19, Name = "冯大涛", Gender = Gender.Male, Generation = "大",
            ParentId = 9,
            BirthDate = new DateTime(1997, 8, 23),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var ggc3 = new Person
        {
            Id = 20, Name = "冯大霞", Gender = Gender.Female, Generation = "大",
            ParentId = 9,
            BirthDate = new DateTime(2000, 2, 15),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 大字辈 - 第四代（冯学魁的子女）
        var ggc4 = new Person
        {
            Id = 21, Name = "冯大林", Gender = Gender.Male, Generation = "大",
            ParentId = 12,
            BirthDate = new DateTime(1998, 6, 9),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var ggc5 = new Person
        {
            Id = 22, Name = "冯芝萱", Gender = Gender.Female, Generation = "大",
            ParentId = 12,
            BirthDate = new DateTime(2001, 10, 3),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var ggc6 = new Person
        {
            Id = 23, Name = "冯大红", Gender = Gender.Female, Generation = "大",
            ParentId = 12,
            BirthDate = new DateTime(2003, 5, 20),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var ggc7 = new Person
        {
            Id = 24, Name = "冯大贵", Gender = Gender.Male, Generation = "大",
            ParentId = 12,
            BirthDate = new DateTime(2005, 12, 1),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 大字辈 - 第四代（冯学荣的子女）
        var ggc8 = new Person
        {
            Id = 25, Name = "冯大宝", Gender = Gender.Male, Generation = "大",
            ParentId = 15,
            BirthDate = new DateTime(2000, 7, 16),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var ggc9 = new Person
        {
            Id = 26, Name = "冯大勇", Gender = Gender.Male, Generation = "大",
            ParentId = 15,
            BirthDate = new DateTime(2002, 9, 25),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var ggc10 = new Person
        {
            Id = 27, Name = "冯大伟", Gender = Gender.Male, Generation = "大",
            ParentId = 15,
            BirthDate = new DateTime(2004, 3, 8),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var ggc11 = new Person
        {
            Id = 28, Name = "冯海玲", Gender = Gender.Female, Generation = "大",
            ParentId = 15,
            BirthDate = new DateTime(2006, 11, 19),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var ggc12 = new Person
        {
            Id = 29, Name = "冯大健", Gender = Gender.Male, Generation = "大",
            ParentId = 15,
            BirthDate = new DateTime(2008, 5, 27),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var ggc13 = new Person
        {
            Id = 30, Name = "冯大发", Gender = Gender.Male, Generation = "大",
            ParentId = 15,
            BirthDate = new DateTime(2010, 1, 13),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 冯学生的子女
        var ggc14 = new Person
        {
            Id = 31, Name = "冯鹏涛", Gender = Gender.Male, Generation = "大",
            ParentId = 17,
            BirthDate = new DateTime(2005, 4, 22),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 鲍小萍 - 冯大辉的配偶
        var ggcSpouse = new Person
        {
            Id = 32, Name = "鲍小萍", Gender = Gender.Female, Generation = "大",
            BirthDate = new DateTime(1997, 2, 10),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 陈慧英 - 冯大林的配偶
        var ggcSpouse2 = new Person
        {
            Id = 33, Name = "陈慧英", Gender = Gender.Female, Generation = "大",
            BirthDate = new DateTime(1999, 8, 5),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 冯大泰 - 冯学滨另一个儿子
        var ggc7Extra = new Person
        {
            Id = 34, Name = "冯大泰", Gender = Gender.Male, Generation = "大",
            ParentId = 9,
            BirthDate = new DateTime(1999, 6, 15),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        // 设置大字辈配偶关系: 冯大辉 <-> 鲍小萍
        ggc1.SpouseId = 32;
        // 冯大林 <-> 陈慧英
        ggc4.SpouseId = 33;

        modelBuilder.Entity<Person>().HasData(
            root, rootSpouse,
            child1, child1Spouse, child2, child2Spouse, child3, child3Spouse,
            gc1, gc1Spouse, gc2, gc3, gc3Spouse, gc4, gc5, gc5Spouse, gc6,
            ggc1, ggc2, ggc3, ggc4, ggc5, ggc6, ggc7, ggc8, ggc9, ggc10, ggc11, ggc12, ggc13, ggc14,
            ggcSpouse, ggcSpouse2, ggc7Extra
        );
    }
}

