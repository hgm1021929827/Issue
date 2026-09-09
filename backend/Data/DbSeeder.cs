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
        EnsureIssueMajorAndSubs(db);
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
                ColorHex = "#8FA8C8",
                SortOrder = 2
            };
            db.MajorCategories.Add(major);
            db.SaveChanges();
        }
        var existing = db.SubCategories.Where(x => x.MajorCategoryId == major.MajorCategoryId).ToList();
        var sort = existing.Select(x => x.SortOrder).DefaultIfEmpty(0).Max();
        (string Name, string Color)[] issueSubs =
        [
            ("處理中", "#7EB8D8"),
            ("加簽", "#E0A86B"),
            ("已結案", "#6BB3A8")
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
