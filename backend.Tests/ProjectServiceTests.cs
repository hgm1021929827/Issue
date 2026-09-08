using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Issue.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Tests;

public class ProjectServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        db.MajorCategories.AddRange(
            new MajorCategory { MajorCategoryId = 1, CategoryName = "議題分類", ColorHex = "#8FA8C8", SortOrder = 1 },
            new MajorCategory { MajorCategoryId = 2, CategoryName = "預約", ColorHex = "#8FA8C8", SortOrder = 2 }
        );
        db.SubCategories.Add(new SubCategory
        {
            SubCategoryId = 11,
            MajorCategoryId = 1,
            SubCategoryName = "進行中",
            ColorHex = "#5BA3C9",
            SortOrder = 1
        });
        db.ClientCompanies.Add(new ClientCompany
        {
            ClientCompanyId = 1,
            CompanyName = "測試客戶",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        return db;
    }

    private static ProjectWriteDto ProjectInput(string code = "P-1", string name = "測試專案") => new()
    {
        Code = code,
        Name = name,
        Description = "",
        MajorCategoryId = 1,
        DueDate = new DateOnly(2026, 9, 20),
        ClientCompanyId = 1
    };

    private static ProjectIssueWriteDto ItemInput(string seq = "1", string title = "議題列") => new()
    {
        SeqNo = seq,
        Title = title,
        Content = "",
        MajorCategoryId = 1
    };

    [Fact]
    public async Task Create_rejects_duplicate_code_ignore_case()
    {
        await using var db = CreateDb();
        var svc = new ProjectService(db);
        await svc.CreateAsync(ProjectInput("ABC"));
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(ProjectInput("abc")));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Seq_unique_within_project_ignore_case_and_ok_across_projects()
    {
        await using var db = CreateDb();
        var svc = new ProjectService(db);
        var a = await svc.CreateAsync(ProjectInput("A"));
        var b = await svc.CreateAsync(ProjectInput("B", "另一專案"));
        await svc.CreateItemAsync(a.Id, ItemInput("X1"));
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.CreateItemAsync(a.Id, ItemInput("x1")));
        Assert.Equal(409, ex.StatusCode);
        var other = await svc.CreateItemAsync(b.Id, ItemInput("X1"));
        Assert.Contains(other, x => x.SeqNo == "X1");
    }

    [Fact]
    public async Task Start_after_due_is_rejected()
    {
        await using var db = CreateDb();
        var svc = new ProjectService(db);
        var input = ProjectInput();
        input.StartDate = new DateOnly(2026, 9, 21);
        input.DueDate = new DateOnly(2026, 9, 20);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(input));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Delete_project_removes_items()
    {
        await using var db = CreateDb();
        var svc = new ProjectService(db);
        var created = await svc.CreateAsync(ProjectInput());
        await svc.CreateItemAsync(created.Id, ItemInput("1"));
        await svc.CreateItemAsync(created.Id, ItemInput("2"));
        var result = await svc.DeleteAsync(created.Id);
        Assert.True(result.Deleted);
        Assert.Equal(2, result.ItemCount);
        Assert.Empty(db.ProjectIssues);
        Assert.Empty(db.Projects);
    }

    [Fact]
    public async Task Wrong_project_path_for_item_is_404()
    {
        await using var db = CreateDb();
        var svc = new ProjectService(db);
        var a = await svc.CreateAsync(ProjectInput("A"));
        var b = await svc.CreateAsync(ProjectInput("B", "另一"));
        var items = await svc.CreateItemAsync(a.Id, ItemInput("1"));
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.UpdateItemAsync(b.Id, items[0].Id, ItemInput("1", "改")));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Calendar_includes_project_and_project_issue_due_not_start_or_plan()
    {
        await using var db = CreateDb();
        var projects = new ProjectService(db);
        var issues = new IssueService(db);
        var project = await projects.CreateAsync(new ProjectWriteDto
        {
            Code = "CAL",
            Name = "月曆專案",
            MajorCategoryId = 1,
            StartDate = new DateOnly(2026, 9, 1),
            DueDate = new DateOnly(2026, 9, 15),
            ClientCompanyId = 1
        });
        await projects.CreateItemAsync(project.Id, new ProjectIssueWriteDto
        {
            SeqNo = "A",
            Title = "專案議題到期",
            MajorCategoryId = 1,
            StartDate = new DateOnly(2026, 9, 2),
            DueDate = new DateOnly(2026, 9, 16)
        });
        var marks = await issues.CalendarAsync(2026, 9);
        Assert.Contains(marks, x => x.Source == "project" && x.Kind == "due" && x.Date == new DateOnly(2026, 9, 15));
        Assert.Contains(marks, x => x.Source == "projectIssue" && x.Kind == "due" && x.Label == "CAL A");
        Assert.DoesNotContain(marks, x => x.Source == "project" && x.Kind == "plan");
        Assert.DoesNotContain(marks, x => x.Source == "project" && x.Date == new DateOnly(2026, 9, 1));
        Assert.DoesNotContain(marks, x => x.Source == "projectIssue" && x.Date == new DateOnly(2026, 9, 2));
    }
}

public class CategoryProjectUsageTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        db.MajorCategories.AddRange(
            new MajorCategory { MajorCategoryId = 1, CategoryName = "議題分類", ColorHex = "#8FA8C8", SortOrder = 1 },
            new MajorCategory { MajorCategoryId = 2, CategoryName = "預約", ColorHex = "#8FA8C8", SortOrder = 2 }
        );
        db.SubCategories.Add(new SubCategory
        {
            SubCategoryId = 11,
            MajorCategoryId = 1,
            SubCategoryName = "進行中",
            ColorHex = "#5BA3C9",
            SortOrder = 1
        });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Delete_sub_rejects_when_used_by_project()
    {
        await using var db = CreateDb();
        db.Projects.Add(new Project
        {
            ProjectCode = "U1",
            ProjectName = "使用中",
            MajorCategoryId = 1,
            SubCategoryId = 11,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        var svc = new CategoryService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.DeleteSubAsync(11));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("專案", ex.Message);
    }

    [Fact]
    public async Task Delete_sub_rejects_when_used_by_project_issue()
    {
        await using var db = CreateDb();
        db.Projects.Add(new Project
        {
            ProjectId = 5,
            ProjectCode = "U2",
            ProjectName = "主檔",
            MajorCategoryId = 2,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.ProjectIssues.Add(new ProjectIssue
        {
            ProjectId = 5,
            SeqNo = "1",
            Title = "列",
            MajorCategoryId = 1,
            SubCategoryId = 11,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        var svc = new CategoryService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.DeleteSubAsync(11));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("專案議題", ex.Message);
    }
}
