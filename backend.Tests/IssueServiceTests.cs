using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Issue.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Tests;

public class IssueServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        db.MajorCategories.Add(new MajorCategory
        {
            MajorCategoryId = 1,
            CategoryName = "議題分類",
            ColorHex = "#91BFD5",
            SortOrder = 1
        });
        db.ClientCompanies.Add(new ClientCompany
        {
            ClientCompanyId = 1,
            CompanyName = "測試客戶",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.Issues.Add(new IssueItem
        {
            IssueId = 1,
            IssueNo = "1",
            Title = "準備連線",
            Content = "",
            MajorCategoryId = 1,
            DueDate = new DateOnly(2026, 9, 10),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Calendar_includes_due_and_plan_kinds()
    {
        await using var db = CreateDb();
        var svc = new IssueService(db);
        await svc.AddPlanAsync(1, new DateOnly(2026, 9, 2));
        var items = await svc.CalendarAsync(2026, 9);
        Assert.Contains(items, x => x.Kind == "due" && x.Date == new DateOnly(2026, 9, 10));
        Assert.Contains(items, x => x.Kind == "plan" && x.Date == new DateOnly(2026, 9, 2));
        Assert.Contains(items, x => x.IssueNo == "1");
    }

    [Fact]
    public async Task AddPlan_rejects_duplicate_day()
    {
        await using var db = CreateDb();
        var svc = new IssueService(db);
        await svc.AddPlanAsync(1, new DateOnly(2026, 9, 2));
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.AddPlanAsync(1, new DateOnly(2026, 9, 2)));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task AddPlans_adds_several_days_and_skips_existing()
    {
        await using var db = CreateDb();
        var svc = new IssueService(db);
        await svc.AddPlanAsync(1, new DateOnly(2026, 9, 2));
        var added = await svc.AddPlansAsync(1, [
            new DateOnly(2026, 9, 2),
            new DateOnly(2026, 9, 3),
            new DateOnly(2026, 9, 4)
        ]);
        Assert.Equal(2, added);
        var items = await svc.CalendarAsync(2026, 9);
        Assert.Contains(items, x => x.Kind == "plan" && x.Date == new DateOnly(2026, 9, 2));
        Assert.Contains(items, x => x.Kind == "plan" && x.Date == new DateOnly(2026, 9, 3));
        Assert.Contains(items, x => x.Kind == "plan" && x.Date == new DateOnly(2026, 9, 4));
    }

    [Fact]
    public async Task AddPlans_range_fills_inclusive_days()
    {
        await using var db = CreateDb();
        var svc = new IssueService(db);
        var added = await svc.AddPlansAsync(1, new IssuePlanWriteDto
        {
            Date = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 9, 3)
        });
        Assert.Equal(3, added);
    }

    [Fact]
    public async Task AddPlans_all_duplicates_throws()
    {
        await using var db = CreateDb();
        var svc = new IssueService(db);
        await svc.AddPlansAsync(1, [new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 3)]);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.AddPlansAsync(1, [new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 3)]));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task RemovePlan_deletes_entry()
    {
        await using var db = CreateDb();
        var svc = new IssueService(db);
        await svc.AddPlanAsync(1, new DateOnly(2026, 9, 2));
        await svc.RemovePlanAsync(1, new DateOnly(2026, 9, 2));
        var items = await svc.CalendarAsync(2026, 9);
        Assert.DoesNotContain(items, x => x.Kind == "plan");
    }

    [Fact]
    public async Task List_filters_by_subCategoryId()
    {
        await using var db = CreateDb();
        db.SubCategories.AddRange(
            new SubCategory
            {
                SubCategoryId = 10,
                MajorCategoryId = 1,
                SubCategoryName = "處理中",
                ColorHex = "#A9D6E8",
                SortOrder = 1
            },
            new SubCategory
            {
                SubCategoryId = 11,
                MajorCategoryId = 1,
                SubCategoryName = "已結案",
                ColorHex = "#A8D8CF",
                SortOrder = 2
            });
        var now = DateTime.Now;
        db.Issues.AddRange(
            new IssueItem
            {
                IssueId = 2,
                IssueNo = "2",
                Title = "處理中單",
                Content = "",
                MajorCategoryId = 1,
                SubCategoryId = 10,
                CreatedAt = now,
                UpdatedAt = now
            },
            new IssueItem
            {
                IssueId = 3,
                IssueNo = "3",
                Title = "已結案單",
                Content = "",
                MajorCategoryId = 1,
                SubCategoryId = 11,
                CreatedAt = now,
                UpdatedAt = now
            });
        db.SaveChanges();
        var svc = new IssueService(db);

        var all = await svc.ListAsync(null);
        Assert.Equal(3, all.Count);

        var inProgress = await svc.ListAsync(null, 10);
        var row = Assert.Single(inProgress);
        Assert.Equal("2", row.IssueNo);

        var unknown = await svc.ListAsync(null, 999);
        Assert.Empty(unknown);
    }

    [Fact]
    public async Task List_filters_by_vendor_and_search()
    {
        await using var db = CreateDb();
        db.ClientCompanies.Add(new ClientCompany
        {
            ClientCompanyId = 2,
            CompanyName = "另一家",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        var now = DateTime.Now;
        db.Issues.AddRange(
            new IssueItem
            {
                IssueId = 2,
                IssueNo = "TP-AA",
                Title = "連線問題",
                Content = "",
                MajorCategoryId = 1,
                ClientCompanyId = 1,
                CreatedAt = now,
                UpdatedAt = now
            },
            new IssueItem
            {
                IssueId = 3,
                IssueNo = "TP-BB",
                Title = "報表調整",
                Content = "",
                MajorCategoryId = 1,
                ClientCompanyId = 2,
                CreatedAt = now,
                UpdatedAt = now
            });
        db.SaveChanges();
        var svc = new IssueService(db);

        var vendor = await svc.ListAsync(null, null, 1);
        var vendorRow = Assert.Single(vendor);
        Assert.Equal("TP-AA", vendorRow.IssueNo);

        var byTitle = await svc.ListAsync(null, null, null, "連線問題");
        var titleRow = Assert.Single(byTitle);
        Assert.Equal("TP-AA", titleRow.IssueNo);

        var byNo = await svc.ListAsync(null, null, null, "tp-bb");
        var noRow = Assert.Single(byNo);
        Assert.Equal("TP-BB", noRow.IssueNo);

        var both = await svc.ListAsync(null, null, 2, "連線問題");
        Assert.Empty(both);
    }

    [Fact]
    public async Task Create_rejects_duplicate_issue_no()
    {
        await using var db = CreateDb();
        var svc = new IssueService(db);
        var input = new IssueWriteDto
        {
            IssueNo = "1",
            Title = "第二筆",
            MajorCategoryId = 1,
            ClientCompanyId = 1
        };
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(input));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Update_allows_same_issue_no()
    {
        await using var db = CreateDb();
        var svc = new IssueService(db);
        var updated = await svc.UpdateAsync(1, new IssueWriteDto
        {
            IssueNo = "1",
            Title = "準備連線",
            MajorCategoryId = 1,
            DueDate = new DateOnly(2026, 9, 10),
            ClientCompanyId = 1
        });
        Assert.Equal("1", updated.IssueNo);
        Assert.Equal(1, updated.ClientCompanyId);
    }

    [Fact]
    public async Task Get_legacy_issue_allows_null_vendor()
    {
        await using var db = CreateDb();
        var svc = new IssueService(db);
        var item = await svc.GetAsync(1);
        Assert.Null(item.ClientCompanyId);
        Assert.Null(item.ClientCompanyName);
    }

    [Fact]
    public async Task Create_rejects_missing_vendor()
    {
        await using var db = CreateDb();
        var svc = new IssueService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(new IssueWriteDto
        {
            IssueNo = "2",
            Title = "無廠商",
            MajorCategoryId = 1
        }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Update_rejects_imported_locked_fields()
    {
        await using var db = CreateDb();
        db.Issues.Single().ImportedFromUof = true;
        db.Issues.Single().ClientCompanyId = 1;
        db.Issues.Single().Title = "準備連線";
        db.Issues.Single().Content = "";
        await db.SaveChangesAsync();
        var svc = new IssueService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.UpdateAsync(1, new IssueWriteDto
        {
            IssueNo = "1",
            Title = "改標題",
            MajorCategoryId = 1,
            DueDate = new DateOnly(2026, 9, 10),
            ClientCompanyId = 1
        }));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("重新匯入", ex.Message);
    }
}
