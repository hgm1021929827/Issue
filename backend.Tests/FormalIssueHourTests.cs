using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Issue.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Tests;

public class FormalIssueHourTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var now = DateTime.Now;
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
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Issues.Add(new IssueItem
        {
            IssueId = 1,
            IssueNo = "IH-1",
            Title = "正式議題工時",
            Content = "",
            MajorCategoryId = 1,
            ClientCompanyId = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Create_list_and_sum()
    {
        await using var db = CreateDb();
        var hours = new WorkHourService(db);
        var list = await hours.CreateForFormalIssueAsync(1, new WorkHourWriteDto
        {
            Date = new DateOnly(2026, 9, 13),
            Hours = 1.5m,
            Remark = "測試"
        });
        Assert.Single(list);
        Assert.Equal(1.5m, list[0].Hours);
        Assert.Equal("測試", list[0].Remark);
        list = await hours.CreateForFormalIssueAsync(1, new WorkHourWriteDto
        {
            Date = new DateOnly(2026, 9, 14),
            Hours = 2m
        });
        Assert.Equal(2, list.Count);
        Assert.Equal(3.5m, list.Sum(x => x.Hours));
    }

    [Fact]
    public async Task Duplicate_day_returns_409()
    {
        await using var db = CreateDb();
        var hours = new WorkHourService(db);
        await hours.CreateForFormalIssueAsync(1, new WorkHourWriteDto
        {
            Date = new DateOnly(2026, 9, 13),
            Hours = 1m
        });
        var ex = await Assert.ThrowsAsync<AppException>(() => hours.CreateForFormalIssueAsync(1, new WorkHourWriteDto
        {
            Date = new DateOnly(2026, 9, 13),
            Hours = 2m
        }));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Delete_issue_removes_hours()
    {
        await using var db = CreateDb();
        var hours = new WorkHourService(db);
        await hours.CreateForFormalIssueAsync(1, new WorkHourWriteDto
        {
            Date = new DateOnly(2026, 9, 13),
            Hours = 1m
        });
        await new IssueService(db).DeleteAsync(1);
        Assert.Empty(db.WorkHours.Where(x => x.IssueId == 1));
        var ex = await Assert.ThrowsAsync<AppException>(() => hours.ListForFormalIssueAsync(1));
        Assert.Equal(404, ex.StatusCode);
    }
}
