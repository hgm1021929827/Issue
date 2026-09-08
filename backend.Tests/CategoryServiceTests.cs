using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Issue.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Tests;

public class CategoryServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        db.MajorCategories.AddRange(
            new MajorCategory { MajorCategoryId = 1, CategoryName = "處理中", ColorHex = "#8FA8C8", SortOrder = 1 },
            new MajorCategory { MajorCategoryId = 2, CategoryName = "加簽中", ColorHex = "#8FA8C8", SortOrder = 2 },
            new MajorCategory { MajorCategoryId = 3, CategoryName = "預約", ColorHex = "#8FA8C8", SortOrder = 3 },
            new MajorCategory { MajorCategoryId = 4, CategoryName = "已完成", ColorHex = "#8FA8C8", SortOrder = 4 }
        );
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task UpdateSubColor_accepts_preset()
    {
        await using var db = CreateDb();
        var svc = new CategoryService(db);
        var created = await svc.CreateSubAsync(new SubCategoryWriteDto { MajorCategoryId = 1, Name = "已發信", Color = "#7EB8D8" });
        var id = created[0].Id;
        var list = await svc.UpdateSubAsync(id, new SubCategoryUpdateDto { Color = "#9B8FBF" });
        Assert.Equal("#9B8FBF", list[0].Color);
    }

    [Fact]
    public async Task UpdateSubColor_rejects_unknown()
    {
        await using var db = CreateDb();
        var svc = new CategoryService(db);
        var created = await svc.CreateSubAsync(new SubCategoryWriteDto { MajorCategoryId = 1, Name = "已發信" });
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.UpdateSubAsync(created[0].Id, new SubCategoryUpdateDto { Color = "#FF0000" }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Seed_does_not_delete_existing_majors()
    {
        using var db = CreateDb();
        DbSeeder.Seed(db);
        var names = db.MajorCategories.Select(x => x.CategoryName).ToList();
        Assert.Contains("處理中", names);
        Assert.Contains("預約", names);
        Assert.Contains("議題", names);
        Assert.DoesNotContain("議題分類", names);
        Assert.Equal(4, db.SubCategories.Count(x => x.MajorCategoryId == 3));
        var issueMajorId = db.MajorCategories.Single(x => x.CategoryName == "議題").MajorCategoryId;
        Assert.Equal(3, db.SubCategories.Count(x => x.MajorCategoryId == issueMajorId));
    }

    [Fact]
    public void Seed_creates_default_majors_when_empty()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new AppDbContext(options);
        DbSeeder.Seed(db);
        var majors = db.MajorCategories.OrderBy(x => x.SortOrder).Select(x => x.CategoryName).ToList();
        Assert.Equal(["預約", "議題"], majors);
        Assert.Equal(7, db.SubCategories.Count());
        Assert.Contains(db.SubCategories, x => x.SubCategoryName == "處理中");
        Assert.Contains(db.SubCategories, x => x.SubCategoryName == "加簽");
        Assert.Contains(db.SubCategories, x => x.SubCategoryName == "已結案");
    }

    [Fact]
    public async Task CreateMajor_rejects_duplicate_name()
    {
        await using var db = CreateDb();
        var svc = new CategoryService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.CreateMajorAsync(new MajorCategoryWriteDto { Name = "預約" }));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateMajor_rejects_duplicate_name()
    {
        await using var db = CreateDb();
        var svc = new CategoryService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.UpdateMajorAsync(1, new MajorCategoryWriteDto { Name = "預約" }));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteMajor_rejects_when_used_by_issue()
    {
        await using var db = CreateDb();
        db.Issues.Add(new IssueItem
        {
            Title = "使用中",
            IssueNo = "u-1",
            Content = "",
            MajorCategoryId = 3,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        var svc = new CategoryService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.DeleteMajorAsync(3));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("使用中", ex.Message);
    }

    [Fact]
    public async Task DeleteMajor_rejects_when_last_remaining()
    {
        await using var db = CreateDb();
        var svc = new CategoryService(db);
        await svc.DeleteMajorAsync(1);
        await svc.DeleteMajorAsync(2);
        await svc.DeleteMajorAsync(4);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.DeleteMajorAsync(3));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("至少", ex.Message);
    }

    [Fact]
    public async Task DeleteMajor_removes_unused_and_keeps_others()
    {
        await using var db = CreateDb();
        var svc = new CategoryService(db);
        var list = await svc.DeleteMajorAsync(1);
        Assert.DoesNotContain(list, x => x.Id == 1);
        Assert.Contains(list, x => x.Name == "預約");
    }
}
