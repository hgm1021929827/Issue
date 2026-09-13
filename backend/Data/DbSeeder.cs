using Issue.Api.Common;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Data;

public static partial class DbSeeder
{
    public static void EnsureDatabase(AppDbContext db)
    {
        db.Database.EnsureCreated();
        EnsureSchema(db);
        Seed(db);
    }

    public static void EnsureSchema(AppDbContext db)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        if (db.Database.IsSqlServer())
        {
            EnsureSchemaSqlServer(db);
        }
        else if (db.Database.IsMySql())
        {
            EnsureSchemaMySql(db);
        }
        else
        {
            throw new InvalidOperationException("不支援的資料庫 Provider（僅支援 SqlServer、MySql）。");
        }
    }

    public static void Seed(AppDbContext db)
    {
        RemapLegacyCategoryColors(db);
        EnsureIssueMajorAndSubs(db);
    }

    /// <summary>將庫內仍為第一階段舊備選色的分類色改寫為新粉彩色盤。</summary>
    public static void RemapLegacyCategoryColors(AppDbContext db)
    {
        var changed = false;
        foreach (var major in db.MajorCategories)
        {
            var mapped = ColorPresets.MapOrDefault(major.ColorHex);
            if (!string.Equals(major.ColorHex, mapped, StringComparison.OrdinalIgnoreCase))
            {
                major.ColorHex = mapped;
                changed = true;
            }
        }
        foreach (var sub in db.SubCategories)
        {
            var mapped = ColorPresets.MapOrDefault(sub.ColorHex);
            if (!string.Equals(sub.ColorHex, mapped, StringComparison.OrdinalIgnoreCase))
            {
                sub.ColorHex = mapped;
                changed = true;
            }
        }
        if (changed)
        {
            db.SaveChanges();
        }
    }

    public static void EnsureIssueMajorAndSubs(AppDbContext db)
    {
        var major = db.MajorCategories.FirstOrDefault(x => x.CategoryName == "議題")
            ?? db.MajorCategories.FirstOrDefault(x => x.CategoryName == "議題分類");
        if (major is null)
        {
            major = new MajorCategory
            {
                CategoryName = "議題",
                ColorHex = ColorPresets.Default,
                SortOrder = 2
            };
            db.MajorCategories.Add(major);
            db.SaveChanges();
        }
        var existing = db.SubCategories.Where(x => x.MajorCategoryId == major.MajorCategoryId).ToList();
        var sort = existing.Select(x => x.SortOrder).DefaultIfEmpty(0).Max();
        (string Name, string Color)[] issueSubs =
        [
            ("處理中", "#A9D6E8"), // 粉藍
            ("加簽", "#EBC5A5"),   // 杏桃橘
            ("已結案", "#A8D8CF")  // 薄荷綠
        ];
        foreach (var (name, color) in issueSubs)
        {
            if (existing.Any(x => x.SubCategoryName == name)) continue;
            sort += 1;
            db.SubCategories.Add(new SubCategory
            {
                MajorCategoryId = major.MajorCategoryId,
                SubCategoryName = name,
                ColorHex = color,
                SortOrder = sort
            });
        }
        db.SaveChanges();
    }
}
