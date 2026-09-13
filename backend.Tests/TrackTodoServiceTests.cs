using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Issue.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Tests;

public class TrackTodoServiceTests
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
        db.ClientCompanies.AddRange(
            new ClientCompany { ClientCompanyId = 1, CompanyName = "甲公司", CreatedAt = now, UpdatedAt = now },
            new ClientCompany { ClientCompanyId = 2, CompanyName = "乙公司", CreatedAt = now, UpdatedAt = now });
        db.ClientContacts.Add(new ClientContact
        {
            ClientContactId = 1,
            ClientCompanyId = 1,
            ContactName = "窗口甲",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.CompanyMembers.Add(new CompanyMember
        {
            CompanyMemberId = 1,
            MemberName = "林大同",
            EnglishName = "Lin",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Issues.Add(new IssueItem
        {
            IssueId = 1,
            IssueNo = "ISS-1",
            Title = "正式議題",
            MajorCategoryId = 1,
            ClientCompanyId = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Projects.Add(new Project
        {
            ProjectId = 1,
            ProjectCode = "P1",
            ProjectName = "專案一",
            MajorCategoryId = 1,
            ClientCompanyId = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.ProjectIssues.Add(new ProjectIssue
        {
            ProjectIssueId = 1,
            ProjectId = 1,
            SeqNo = "1",
            Title = "專案議題",
            MajorCategoryId = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.SaveChanges();
        return db;
    }

    private static TrackTodoWriteDto MemberWrite(string title = "追林大同") => new()
    {
        Title = title,
        TargetType = TrackTodoService.TargetMember,
        TargetId = 1
    };

    private static TrackTodoWriteDto ContactWrite(string title = "追窗口") => new()
    {
        Title = title,
        TargetType = TrackTodoService.TargetContact,
        TargetId = 1
    };

    [Fact]
    public async Task Create_member_on_issue_ok()
    {
        await using var db = CreateDb();
        var svc = new TrackTodoService(db);
        var list = await svc.CreateForIssueAsync(1, MemberWrite());
        Assert.Single(list);
        Assert.Equal("issue", list[0].WorkType);
        Assert.Equal(1, list[0].WorkId);
        Assert.Equal("林大同", list[0].TargetName);
        Assert.Equal("Lin", list[0].TargetSecondaryName);
        Assert.False(list[0].IsCompleted);
    }

    [Fact]
    public async Task Create_contact_without_vendor_is_400()
    {
        await using var db = CreateDb();
        db.Issues.Find(1L)!.ClientCompanyId = null;
        await db.SaveChangesAsync();
        var svc = new TrackTodoService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.CreateForIssueAsync(1, ContactWrite()));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("請先設定廠商", ex.Message);
    }

    [Fact]
    public async Task Create_contact_without_windows_is_400()
    {
        await using var db = CreateDb();
        db.ClientContacts.RemoveRange(db.ClientContacts);
        await db.SaveChangesAsync();
        var svc = new TrackTodoService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.CreateForIssueAsync(1, ContactWrite()));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("請先在成員維護新增客戶窗口", ex.Message);
    }

    [Fact]
    public async Task Create_contact_from_other_company_is_400()
    {
        await using var db = CreateDb();
        db.ClientContacts.Add(new ClientContact
        {
            ClientContactId = 2,
            ClientCompanyId = 2,
            ContactName = "窗口乙",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        var svc = new TrackTodoService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.CreateForIssueAsync(1, new TrackTodoWriteDto
            {
                Title = "錯公司",
                TargetType = TrackTodoService.TargetContact,
                TargetId = 2
            }));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("客戶窗口須屬於該工作的客戶公司", ex.Message);
    }

    [Fact]
    public async Task Blank_title_is_400()
    {
        await using var db = CreateDb();
        var svc = new TrackTodoService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.CreateForIssueAsync(1, new TrackTodoWriteDto
            {
                Title = "  ",
                TargetType = TrackTodoService.TargetMember,
                TargetId = 1
            }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Home_lists_incomplete_only()
    {
        await using var db = CreateDb();
        var svc = new TrackTodoService(db);
        await svc.CreateForIssueAsync(1, MemberWrite("未完成"));
        var created = await svc.CreateForProjectAsync(1, MemberWrite("將完成"));
        await svc.SetCompletedAsync(created[0].Id, true);
        var home = await svc.ListHomeAsync();
        Assert.Single(home);
        Assert.Equal("未完成", home[0].Title);
        Assert.Contains("ISS-1", home[0].WorkLabel);
    }

    [Fact]
    public async Task Calendar_includes_incomplete_reminder_only()
    {
        await using var db = CreateDb();
        var tracks = new TrackTodoService(db);
        var issues = new IssueService(db);
        await tracks.CreateForIssueAsync(1, new TrackTodoWriteDto
        {
            Title = "九月提醒",
            TargetType = TrackTodoService.TargetMember,
            TargetId = 1,
            ReminderDate = new DateOnly(2026, 9, 10)
        });
        await tracks.CreateForIssueAsync(1, new TrackTodoWriteDto
        {
            Title = "無日期",
            TargetType = TrackTodoService.TargetMember,
            TargetId = 1
        });
        var done = await tracks.CreateForIssueAsync(1, new TrackTodoWriteDto
        {
            Title = "已完成仍有日",
            TargetType = TrackTodoService.TargetMember,
            TargetId = 1,
            ReminderDate = new DateOnly(2026, 9, 11)
        });
        await tracks.SetCompletedAsync(done[^1].Id, true);
        var afterDone = await tracks.ListForIssueAsync(1);
        Assert.Equal(new DateOnly(2026, 9, 11), afterDone.First(x => x.Title == "已完成仍有日").ReminderDate);
        await tracks.CreateForIssueAsync(1, new TrackTodoWriteDto
        {
            Title = "十月提醒",
            TargetType = TrackTodoService.TargetMember,
            TargetId = 1,
            ReminderDate = new DateOnly(2026, 10, 1)
        });
        var home = await tracks.ListHomeAsync();
        Assert.Contains(home, x => x.Title == "九月提醒" && x.ReminderDate == new DateOnly(2026, 9, 10));
        Assert.DoesNotContain(home, x => x.Title == "已完成仍有日");
        var marks = await issues.CalendarAsync(2026, 9);
        Assert.Contains(marks, x => x.Source == "trackTodo" && x.Kind == "track" && x.Title == "九月提醒" && x.Date == new DateOnly(2026, 9, 10));
        Assert.DoesNotContain(marks, x => x.Title == "無日期");
        Assert.DoesNotContain(marks, x => x.Title == "已完成仍有日");
        Assert.DoesNotContain(marks, x => x.Title == "十月提醒");
        var hit = marks.First(x => x.Title == "九月提醒");
        Assert.Equal("issue", hit.WorkType);
        Assert.Equal(1, hit.WorkId);
    }

    [Fact]
    public async Task Reorder_rejects_partial_list()
    {
        await using var db = CreateDb();
        var svc = new TrackTodoService(db);
        await svc.CreateForIssueAsync(1, MemberWrite("A"));
        var list = await svc.CreateForIssueAsync(1, MemberWrite("B"));
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.ReorderAsync(list[0].Id, new TrackTodoReorderDto { OrderedIds = [list[0].Id] }));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Reorder_persists_order()
    {
        await using var db = CreateDb();
        var svc = new TrackTodoService(db);
        await svc.CreateForIssueAsync(1, MemberWrite("A"));
        var list = await svc.CreateForIssueAsync(1, MemberWrite("B"));
        var ids = list.Select(x => x.Id).Reverse().ToList();
        var reordered = await svc.ReorderAsync(ids[0], new TrackTodoReorderDto { OrderedIds = ids });
        Assert.Equal(ids, reordered.Select(x => x.Id).ToList());
    }

    [Fact]
    public async Task Delete_issue_removes_track_todos()
    {
        await using var db = CreateDb();
        var tracks = new TrackTodoService(db);
        await tracks.CreateForIssueAsync(1, MemberWrite());
        var issues = new IssueService(db);
        await issues.DeleteAsync(1);
        Assert.Empty(db.TrackTodos);
    }

    [Fact]
    public async Task Change_issue_vendor_blocked_when_contact_track_exists()
    {
        await using var db = CreateDb();
        var tracks = new TrackTodoService(db);
        await tracks.CreateForIssueAsync(1, ContactWrite());
        var issues = new IssueService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => issues.UpdateAsync(1, new IssueWriteDto
        {
            IssueNo = "ISS-1",
            Title = "正式議題",
            MajorCategoryId = 1,
            ClientCompanyId = 2
        }));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("客戶窗口", ex.Message);
    }

    [Fact]
    public async Task Change_issue_vendor_ok_when_only_member_track()
    {
        await using var db = CreateDb();
        var tracks = new TrackTodoService(db);
        await tracks.CreateForIssueAsync(1, MemberWrite());
        var issues = new IssueService(db);
        var updated = await issues.UpdateAsync(1, new IssueWriteDto
        {
            IssueNo = "ISS-1",
            Title = "正式議題",
            MajorCategoryId = 1,
            ClientCompanyId = 2
        });
        Assert.Equal(2, updated.ClientCompanyId);
    }

    [Fact]
    public async Task Change_project_vendor_counts_item_tracks()
    {
        await using var db = CreateDb();
        var tracks = new TrackTodoService(db);
        await tracks.CreateForProjectItemAsync(1, 1, ContactWrite());
        var projects = new ProjectService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => projects.UpdateAsync(1, new ProjectWriteDto
        {
            Code = "P1",
            Name = "專案一",
            MajorCategoryId = 1,
            ClientCompanyId = 2
        }));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("專案議題", ex.Message);
    }

    [Fact]
    public async Task Delete_contact_in_use_is_409()
    {
        await using var db = CreateDb();
        var tracks = new TrackTodoService(db);
        await tracks.CreateForIssueAsync(1, ContactWrite());
        var dir = new DirectoryService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => dir.DeleteContactAsync(1, 1));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("需要追蹤的 TODO", ex.Message);
    }

    [Fact]
    public async Task Delete_member_in_use_as_track_is_409()
    {
        await using var db = CreateDb();
        var tracks = new TrackTodoService(db);
        await tracks.CreateForIssueAsync(1, MemberWrite());
        var dir = new DirectoryService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => dir.DeleteMemberAsync(1));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("需要追蹤的 TODO", ex.Message);
    }

    [Fact]
    public async Task Delete_company_in_use_by_track_is_409()
    {
        await using var db = CreateDb();
        var tracks = new TrackTodoService(db);
        await tracks.CreateForIssueAsync(1, ContactWrite());
        db.Issues.Find(1L)!.ClientCompanyId = 2;
        await db.SaveChangesAsync();
        var dir = new DirectoryService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => dir.DeleteCompanyAsync(1));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("需要追蹤的 TODO", ex.Message);
    }

    [Fact]
    public async Task Project_item_create_and_home_label()
    {
        await using var db = CreateDb();
        var svc = new TrackTodoService(db);
        await svc.CreateForProjectItemAsync(1, 1, MemberWrite("項次追蹤"));
        var home = await svc.ListHomeAsync();
        Assert.Equal("projectIssue", home[0].WorkType);
        Assert.Equal(1, home[0].ProjectId);
        Assert.Contains("P1", home[0].WorkLabel);
        Assert.Contains("1", home[0].WorkLabel);
    }

    [Fact]
    public async Task Wrong_project_item_path_is_404()
    {
        await using var db = CreateDb();
        var svc = new TrackTodoService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.ListForProjectItemAsync(99, 1));
        Assert.Equal(404, ex.StatusCode);
    }
}
