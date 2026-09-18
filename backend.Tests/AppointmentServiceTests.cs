using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Issue.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Tests;

public class AppointmentServiceTests
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
            CategoryName = "預約",
            ColorHex = "#8FA8C8",
            SortOrder = 1
        });
        db.SubCategories.AddRange(
            new SubCategory { SubCategoryId = 11, MajorCategoryId = 1, SubCategoryName = "已發信", ColorHex = "#91BFD5", SortOrder = 1 },
            new SubCategory { SubCategoryId = 12, MajorCategoryId = 1, SubCategoryName = "連線完成", ColorHex = "#91BFD5", SortOrder = 2 },
            new SubCategory { SubCategoryId = 13, MajorCategoryId = 1, SubCategoryName = "連線取消", ColorHex = "#91BFD5", SortOrder = 3 }
        );
        db.ClientCompanies.Add(new ClientCompany
        {
            ClientCompanyId = 5,
            CompanyName = "甲客戶",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.ClientContacts.Add(new ClientContact
        {
            ClientContactId = 6,
            ClientCompanyId = 5,
            ContactName = "窗口A",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.Issues.Add(new IssueItem
        {
            IssueId = 20,
            IssueNo = "20",
            Title = "正式議題",
            Content = "",
            MajorCategoryId = 1,
            ClientCompanyId = 5,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.Projects.Add(new Project
        {
            ProjectId = 30,
            ProjectCode = "P-1",
            ProjectName = "專案一",
            Description = "",
            MajorCategoryId = 1,
            ClientCompanyId = 5,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Create_and_list_includes_issue_item()
    {
        await using var db = CreateDb();
        var svc = new AppointmentService(db);
        var created = await svc.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            SubCategoryId = 11,
            AppointmentDate = new DateOnly(2099, 1, 2),
            Items = [new AppointmentItemWriteDto { IssueId = 20 }]
        });
        Assert.Equal("甲客戶", created.ClientCompanyName);
        Assert.Equal("窗口A", created.ClientContactName);
        Assert.Equal("已發信", created.StatusName);
        Assert.Single(created.Items);
        Assert.Equal("issue", created.Items[0].Kind);
        var list = await svc.ListAsync();
        Assert.Single(list);
    }

    [Fact]
    public async Task Window_must_belong_to_company()
    {
        await using var db = CreateDb();
        db.ClientCompanies.Add(new ClientCompany
        {
            ClientCompanyId = 7,
            CompanyName = "乙客戶",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        var svc = new AppointmentService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 7,
            ClientContactId = 6,
            SubCategoryId = 11
        }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Future_open_appointments_exclude_ended_and_undated()
    {
        await using var db = CreateDb();
        var svc = new AppointmentService(db);
        await svc.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            SubCategoryId = 11,
            AppointmentDate = DateOnly.FromDateTime(DateTime.Now).AddDays(3)
        });
        await svc.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            SubCategoryId = 12,
            AppointmentDate = DateOnly.FromDateTime(DateTime.Now).AddDays(3)
        });
        await svc.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            SubCategoryId = 11
        });
        var future = await svc.ListFutureAsync(5, null);
        Assert.Single(future);
        Assert.Equal("已發信", future[0].StatusName);
    }

    [Fact]
    public async Task Duplicate_item_is_409()
    {
        await using var db = CreateDb();
        var svc = new AppointmentService(db);
        var created = await svc.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            Items = [new AppointmentItemWriteDto { ProjectId = 30 }]
        });
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.AddItemsAsync(created.Id, new AppointmentItemsWriteDto
            {
                Items = [new AppointmentItemWriteDto { ProjectId = 30 }]
            }));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Work_item_todo_cannot_be_item()
    {
        await using var db = CreateDb();
        db.ProjectWorkItems.Add(new ProjectWorkItem
        {
            ProjectWorkItemId = 40,
            ProjectId = 30,
            WorkItemCode = "W-1",
            Title = "項次",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.IssueTodos.Add(new IssueTodo
        {
            TodoId = 50,
            ProjectWorkItemId = 40,
            Title = "項次待辦",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        var svc = new AppointmentService(db);
        var created = await svc.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6
        });
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.AddItemsAsync(created.Id, new AppointmentItemsWriteDto
            {
                Items = [new AppointmentItemWriteDto { TodoId = 50 }]
            }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Calendar_includes_dated_appointment_only()
    {
        await using var db = CreateDb();
        var appointments = new AppointmentService(db);
        await appointments.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            SubCategoryId = 11,
            AppointmentDate = new DateOnly(2099, 6, 15)
        });
        await appointments.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            SubCategoryId = 11
        });
        var issues = new IssueService(db);
        var marks = await issues.CalendarAsync(2099, 6);
        Assert.Contains(marks, x => x.Source == "appointment" && x.Title == "甲客戶");
        Assert.DoesNotContain(marks, x => x.Source == "appointment" && x.Date != new DateOnly(2099, 6, 15));
    }

    [Fact]
    public async Task Delete_contact_in_use_is_409()
    {
        await using var db = CreateDb();
        var appointments = new AppointmentService(db);
        await appointments.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            SubCategoryId = 11
        });
        var directory = new DirectoryService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => directory.DeleteContactAsync(5, 6));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("預約連線", ex.Message);
    }

    [Fact]
    public async Task Channel_must_belong_to_selected_contact()
    {
        await using var db = CreateDb();
        db.ClientContacts.Add(new ClientContact
        {
            ClientContactId = 8,
            ClientCompanyId = 5,
            ContactName = "窗口B",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.ContactChannels.Add(new ContactChannel
        {
            ContactChannelId = 9,
            ClientContactId = 8,
            ChannelType = "mobile",
            ChannelValue = "0911111111"
        });
        db.SaveChanges();
        var svc = new AppointmentService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            ContactChannelId = 9
        }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task List_shows_channel_label()
    {
        await using var db = CreateDb();
        db.ContactChannels.Add(new ContactChannel
        {
            ContactChannelId = 7,
            ClientContactId = 6,
            ChannelType = "mobile",
            ChannelValue = "0912345678"
        });
        db.SaveChanges();
        var svc = new AppointmentService(db);
        var created = await svc.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            ContactChannelId = 7
        });
        Assert.Equal("手機 0912345678", created.ContactChannelLabel);
    }

    [Fact]
    public async Task Delete_channel_in_use_is_409()
    {
        await using var db = CreateDb();
        db.ContactChannels.Add(new ContactChannel
        {
            ContactChannelId = 7,
            ClientContactId = 6,
            ChannelType = "phone",
            ChannelValue = "02-123"
        });
        db.SaveChanges();
        var appointments = new AppointmentService(db);
        await appointments.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            ContactChannelId = 7
        });
        var directory = new DirectoryService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => directory.DeleteChannelAsync(5, 6, 7));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("預約連線", ex.Message);
    }

    [Fact]
    public async Task Deleting_issue_keeps_appointment_and_drops_item()
    {
        await using var db = CreateDb();
        var appointments = new AppointmentService(db);
        var created = await appointments.CreateAsync(new AppointmentWriteDto
        {
            ClientCompanyId = 5,
            ClientContactId = 6,
            Items = [new AppointmentItemWriteDto { IssueId = 20 }]
        });
        var issues = new IssueService(db);
        await issues.DeleteAsync(20);
        var row = await appointments.GetAsync(created.Id);
        Assert.Empty(row.Items);
    }
}
