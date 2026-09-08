using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Issue.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Tests;

public class DirectoryServiceTests
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
            ColorHex = "#8FA8C8",
            SortOrder = 1
        });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Company_name_unique_ignore_case()
    {
        await using var db = CreateDb();
        var svc = new DirectoryService(db);
        await svc.CreateCompanyAsync(new ClientCompanyWriteDto { Name = "Acme" });
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.CreateCompanyAsync(new ClientCompanyWriteDto { Name = "acme" }));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Contact_name_unique_within_company_ok_across_companies()
    {
        await using var db = CreateDb();
        var svc = new DirectoryService(db);
        var a = await svc.CreateCompanyAsync(new ClientCompanyWriteDto { Name = "甲公司" });
        var b = await svc.CreateCompanyAsync(new ClientCompanyWriteDto { Name = "乙公司" });
        await svc.CreateContactAsync(a.Id, new ClientContactWriteDto { Name = "王小明" });
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.CreateContactAsync(a.Id, new ClientContactWriteDto { Name = "王小明" }));
        Assert.Equal(409, ex.StatusCode);
        var other = await svc.CreateContactAsync(b.Id, new ClientContactWriteDto { Name = "王小明" });
        Assert.Contains(other, x => x.Name == "王小明");
    }

    [Fact]
    public async Task Member_chinese_name_may_duplicate()
    {
        await using var db = CreateDb();
        var svc = new DirectoryService(db);
        await svc.CreateMemberAsync(new CompanyMemberWriteDto { Name = "林大同", EnglishName = "Lin" });
        var second = await svc.CreateMemberAsync(new CompanyMemberWriteDto { Name = "林大同", EnglishName = "David" });
        Assert.Equal("林大同", second.Name);
        Assert.Equal(2, (await svc.ListMembersAsync(null)).Count);
    }

    [Fact]
    public async Task Shortcut_create_company_returns_id()
    {
        await using var db = CreateDb();
        var svc = new DirectoryService(db);
        var created = await svc.CreateCompanyAsync(new ClientCompanyWriteDto { Name = "快捷客戶" });
        Assert.True(created.Id > 0);
        Assert.Equal("快捷客戶", created.Name);
    }

    [Fact]
    public async Task Delete_company_in_use_is_409()
    {
        await using var db = CreateDb();
        var dir = new DirectoryService(db);
        var company = await dir.CreateCompanyAsync(new ClientCompanyWriteDto { Name = "使用中公司" });
        db.Projects.Add(new Project
        {
            ProjectCode = "P1",
            ProjectName = "專案",
            MajorCategoryId = 1,
            ClientCompanyId = company.Id,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        var ex = await Assert.ThrowsAsync<AppException>(() => dir.DeleteCompanyAsync(company.Id));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("專案", ex.Message);
    }

    [Fact]
    public async Task Delete_member_in_use_is_409()
    {
        await using var db = CreateDb();
        var dir = new DirectoryService(db);
        var member = await dir.CreateMemberAsync(new CompanyMemberWriteDto { Name = "負責人", EnglishName = "Owner" });
        db.Projects.Add(new Project
        {
            ProjectCode = "P2",
            ProjectName = "專案",
            MajorCategoryId = 1,
            OwnerMemberId = member.Id,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        var ex = await Assert.ThrowsAsync<AppException>(() => dir.DeleteMemberAsync(member.Id));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("專案", ex.Message);
    }

    [Fact]
    public async Task Delete_company_cascades_contacts()
    {
        await using var db = CreateDb();
        var svc = new DirectoryService(db);
        var company = await svc.CreateCompanyAsync(new ClientCompanyWriteDto { Name = "可刪公司" });
        await svc.CreateContactAsync(company.Id, new ClientContactWriteDto { Name = "窗口" });
        await svc.DeleteCompanyAsync(company.Id);
        Assert.Empty(await svc.TreeAsync(null));
        Assert.Empty(db.ClientContacts);
    }

    [Fact]
    public async Task Tree_keyword_keeps_company_when_contact_matches()
    {
        await using var db = CreateDb();
        var svc = new DirectoryService(db);
        var company = await svc.CreateCompanyAsync(new ClientCompanyWriteDto { Name = "甲公司" });
        await svc.CreateCompanyAsync(new ClientCompanyWriteDto { Name = "乙公司" });
        await svc.CreateContactAsync(company.Id, new ClientContactWriteDto { Name = "窗口小花" });
        var tree = await svc.TreeAsync("小花");
        Assert.Single(tree);
        Assert.Equal("甲公司", tree[0].Name);
    }
}

public class ProjectVendorTests
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
            ColorHex = "#8FA8C8",
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

    [Fact]
    public async Task Create_project_rejects_missing_vendor()
    {
        await using var db = CreateDb();
        var svc = new ProjectService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(new ProjectWriteDto
        {
            Code = "NV",
            Name = "無廠商",
            MajorCategoryId = 1
        }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Get_legacy_project_allows_null_vendor()
    {
        await using var db = CreateDb();
        db.Projects.Add(new Project
        {
            ProjectId = 9,
            ProjectCode = "OLD",
            ProjectName = "舊專案",
            MajorCategoryId = 1,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        var item = await new ProjectService(db).GetAsync(9);
        Assert.Null(item.ClientCompanyId);
        Assert.Null(item.ClientCompanyName);
    }

    [Fact]
    public async Task Project_issue_write_does_not_store_own_vendor()
    {
        await using var db = CreateDb();
        var svc = new ProjectService(db);
        var project = await svc.CreateAsync(new ProjectWriteDto
        {
            Code = "PV",
            Name = "有廠商",
            MajorCategoryId = 1,
            ClientCompanyId = 1
        });
        var items = await svc.CreateItemAsync(project.Id, new ProjectIssueWriteDto
        {
            SeqNo = "1",
            Title = "列",
            MajorCategoryId = 1
        });
        Assert.Equal(1, items[0].ClientCompanyId);
        Assert.Equal("測試客戶", items[0].ClientCompanyName);
        Assert.Null(typeof(ProjectIssueWriteDto).GetProperty("ClientCompanyId"));
        Assert.All(db.ProjectIssues, x => Assert.Equal(project.Id, x.ProjectId));
    }
}
