using System.Text;
using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Issue.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Tests;

public class IssueImportTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var now = DateTime.Now;
        db.MajorCategories.Add(new MajorCategory { MajorCategoryId = 1, CategoryName = "議題", ColorHex = "#91BFD5", SortOrder = 1 });
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
            },
            new SubCategory
            {
                SubCategoryId = 12,
                MajorCategoryId = 1,
                SubCategoryName = "加簽",
                ColorHex = "#EBC5A5",
                SortOrder = 3
            });
        db.ClientCompanies.Add(new ClientCompany
        {
            ClientCompanyId = 1,
            CompanyName = "亞家科技",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.CompanyMembers.Add(new CompanyMember
        {
            CompanyMemberId = 1,
            MemberName = "蕭維德",
            EnglishName = "Wade",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.SaveChanges();
        return db;
    }

    private static IssueImportService Service(AppDbContext db, ImportNotFoundCache? cache = null) =>
        new(db, cache ?? new ImportNotFoundCache());

    [Fact]
    public void Parser_decodes_numeric_header_entities()
    {
        Assert.Equal("預計完成日", UofIssueHtmlParser.DecodeHeader("&#38928;&#35336;&#23436;&#25104;&#26085;"));
        Assert.Equal("工程師-1", UofIssueHtmlParser.DecodeHeader("&#24037;&#31243;&#24107;-1"));
        Assert.Equal("議題標題", UofIssueHtmlParser.DecodeHeader("&#35696;&#38988;&#27161;&#38988;"));
        Assert.Equal("美食家食材通路股份有限公司", UofIssueHtmlParser.ParseVendorName("客戶名稱 : 美食家食材通路股份有限公司"));
        Assert.Equal("亞家科技", UofIssueHtmlParser.ParseVendorName("客戶名稱：亞家科技"));
    }

    [Fact]
    public async Task Import_filters_sa_or_engineer_and_upserts()
    {
        await using var db = CreateDb();
        db.Issues.Add(new IssueItem
        {
            IssueNo = "TP1",
            Title = "舊標題",
            Content = "舊內容",
            Remark = "勿蓋",
            MajorCategoryId = 1,
            SubCategoryId = 10,
            ClientCompanyId = 1,
            ImportedFromUof = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.IssueTodos.Add(new IssueTodo
        {
            IssueId = db.Issues.Single().IssueId,
            Title = "既有 TODO",
            Content = "",
            SortOrder = 1,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var html = UofHtml(
            Row("TP1", "新標題", "新內容", "2026/09/04", "客戶名稱 : 亞家科技", "處理中", "蕭維德(J007)", "", ""),
            Row("TP2", "工程師列", "內容二", "2026-09-10", "客戶名稱 : 新客戶公司", "結案(通過)", "", "wade", ""),
            Row("TP3", "未命中", "x", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "張楷正", "", ""));
        var result = await Service(db).ImportAsync(HtmlFile(html), 1, 10, 12, 11);

        Assert.Equal(1, result.Summary.Created);
        Assert.Equal(1, result.Summary.Updated);
        Assert.Equal(1, result.Summary.Skipped);
        Assert.Contains(result.CreatedCompanies, x => x.Name == "新客戶公司");
        var updated = db.Issues.Single(x => x.IssueNo == "TP1");
        Assert.Equal("新標題", updated.Title);
        Assert.Equal("勿蓋", updated.Remark);
        Assert.Equal(10, updated.SubCategoryId);
        Assert.False(updated.MissingKept);
        Assert.True(updated.ImportedFromUof);
        Assert.Equal("既有 TODO", db.IssueTodos.Single().Title);
        var created = db.Issues.Single(x => x.IssueNo == "TP2");
        Assert.Equal(1, created.MajorCategoryId);
        Assert.Equal(11, created.SubCategoryId);
        Assert.DoesNotContain(db.Issues, x => x.IssueNo == "TP3");
    }

    [Fact]
    public async Task Import_row_errors_and_closed_clears_foreign_subcategory()
    {
        await using var db = CreateDb();
        db.Issues.Add(new IssueItem
        {
            IssueNo = "OLD",
            Title = "舊",
            Content = "",
            MajorCategoryId = 1,
            SubCategoryId = 10,
            ClientCompanyId = 1,
            ImportedFromUof = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        var html = UofHtml(
            Row("", "無編號", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "蕭維德", "", ""),
            Row("A", "", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "蕭維德", "", ""),
            Row("B", "無廠商", "c", "2026/09/01", "", "處理中", "蕭維德", "", ""),
            Row("C", "重複", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "蕭維德", "", ""),
            Row("C", "重複後列", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "蕭維德", "", ""),
            Row("D", "壞日期", "c", "不是日期", "客戶名稱 : 亞家科技", "處理中", "蕭維德", "", ""),
            Row("OLD", "結案更新", "c", "2026/09/01", "客戶名稱 : 亞家科技", "結案(作廢)", "蕭維德", "", ""));
        var result = await Service(db).ImportAsync(HtmlFile(html), 1, 10, 12, 11);
        Assert.Contains(result.RowErrors, x => x.Kind == "missingIssueNo" && x.RowIndex == 1);
        Assert.Contains(result.RowErrors, x => x.Kind == "missingTitle");
        Assert.Contains(result.RowErrors, x => x.Kind == "missingVendor");
        Assert.Contains(result.RowErrors, x => x.Kind == "duplicateIssueNo");
        Assert.Contains(result.RowErrors, x => x.Kind == "badDate");
        var closed = db.Issues.Single(x => x.IssueNo == "OLD");
        Assert.Equal(1, closed.MajorCategoryId);
        Assert.Equal(11, closed.SubCategoryId);
    }

    [Fact]
    public async Task Import_sets_sub_when_missing_and_keeps_when_open()
    {
        await using var db = CreateDb();
        db.Issues.Add(new IssueItem
        {
            IssueNo = "EMPTY",
            Title = "無小分類",
            Content = "",
            MajorCategoryId = 1,
            SubCategoryId = null,
            ClientCompanyId = 1,
            ImportedFromUof = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.Issues.Add(new IssueItem
        {
            IssueNo = "KEEP",
            Title = "有小分類",
            Content = "",
            MajorCategoryId = 1,
            SubCategoryId = 10,
            ClientCompanyId = 1,
            ImportedFromUof = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        var html = UofHtml(
            Row("EMPTY", "無小分類", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "蕭維德", "", ""),
            Row("KEEP", "有小分類", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "蕭維德", "", ""));
        await Service(db).ImportAsync(HtmlFile(html), 1, 10, 12, 11);
        Assert.Equal(10, db.Issues.Single(x => x.IssueNo == "EMPTY").SubCategoryId);
        Assert.Equal(10, db.Issues.Single(x => x.IssueNo == "KEEP").SubCategoryId);
    }

    [Fact]
    public async Task Import_closed_overwrites_existing_sub()
    {
        await using var db = CreateDb();
        db.Issues.Add(new IssueItem
        {
            IssueNo = "OLD",
            Title = "將結案",
            Content = "",
            MajorCategoryId = 1,
            SubCategoryId = 10,
            ClientCompanyId = 1,
            ImportedFromUof = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        var html = UofHtml(Row("OLD", "將結案", "c", "2026/09/01", "客戶名稱 : 亞家科技", "結案(通過)", "蕭維德", "", ""));
        await Service(db).ImportAsync(HtmlFile(html), 1, 10, 12, 11);
        Assert.Equal(11, db.Issues.Single(x => x.IssueNo == "OLD").SubCategoryId);
    }

    [Fact]
    public async Task Import_assigns_sub_by_engineer_and_signer()
    {
        await using var db = CreateDb();
        var now = DateTime.Now;
        db.Issues.AddRange(
            new IssueItem
            {
                IssueNo = "E1",
                Title = "舊加簽",
                Content = "",
                MajorCategoryId = 1,
                SubCategoryId = 10,
                ClientCompanyId = 1,
                ImportedFromUof = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new IssueItem
            {
                IssueNo = "E2",
                Title = "舊結案",
                Content = "",
                MajorCategoryId = 1,
                SubCategoryId = 10,
                ClientCompanyId = 1,
                ImportedFromUof = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new IssueItem
            {
                IssueNo = "E3",
                Title = "簽核者本人",
                Content = "",
                MajorCategoryId = 1,
                SubCategoryId = 12,
                ClientCompanyId = 1,
                ImportedFromUof = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        await db.SaveChangesAsync();
        var html = UofHtml(
            Row("N1", "新建他人簽核", "c", "2026/09/01", "客戶名稱 : 亞家科技", "結案(通過)", "", "蕭維德", "", "專案組 張楷正"),
            Row("N2", "新建本人簽核", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "", "Wade", "", "專案組 蕭維德"),
            Row("E1", "既有加簽", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "", "蕭維德", "", "專案組 張楷正"),
            Row("E2", "既有結案", "c", "2026/09/01", "客戶名稱 : 亞家科技", "結案(作廢)", "", "蕭維德", "", ""),
            Row("E3", "回到手上", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "", "蕭維德", "", "專案組 蕭維德"));
        var result = await Service(db).ImportAsync(HtmlFile(html), 1, 10, 12, 11);
        Assert.Equal(11, db.Issues.Single(x => x.IssueNo == "N1").SubCategoryId);
        Assert.Equal(10, db.Issues.Single(x => x.IssueNo == "N2").SubCategoryId);
        Assert.Equal(10, db.Issues.Single(x => x.IssueNo == "E1").SubCategoryId);
        Assert.Equal(11, db.Issues.Single(x => x.IssueNo == "E2").SubCategoryId);
        Assert.Equal(10, db.Issues.Single(x => x.IssueNo == "E3").SubCategoryId);
        Assert.Single(result.PendingCategory);
        Assert.Contains(result.PendingCategory, x => x.IssueNo == "E1" && x.Status == "處理中");
        Assert.DoesNotContain(result.PendingCategory, x => x.IssueNo == "E2");
        Assert.DoesNotContain(result.PendingCategory, x => x.IssueNo == "E3");
    }

    [Fact]
    public async Task Decisions_apply_manual_countersign_or_done()
    {
        await using var db = CreateDb();
        var cache = new ImportNotFoundCache();
        var svc = Service(db, cache);
        var now = DateTime.Now;
        db.Issues.Add(new IssueItem
        {
            IssueNo = "E1",
            Title = "既有",
            Content = "",
            MajorCategoryId = 1,
            SubCategoryId = 10,
            ClientCompanyId = 1,
            ImportedFromUof = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
        var html = UofHtml(Row("E1", "既有", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "", "蕭維德", "", "專案組 張楷正"));
        var result = await svc.ImportAsync(HtmlFile(html), 1, 10, 12, 11);
        var pending = Assert.Single(result.PendingCategory);
        var empty = await Assert.ThrowsAsync<AppException>(() =>
            svc.ApplyDecisionsAsync(new IssueImportDecisionRequestDto { Decisions = [], CategoryChoices = [] }));
        Assert.Equal("請為每筆選擇加簽或結案", empty.Message);
        await svc.ApplyDecisionsAsync(new IssueImportDecisionRequestDto
        {
            CategoryChoices = [new IssueImportDecisionItemDto { Id = pending.Id, Action = "countersign" }]
        });
        Assert.Equal(12, db.Issues.Single(x => x.IssueNo == "E1").SubCategoryId);
    }

    [Fact]
    public async Task Import_rejects_same_or_missing_subs()
    {
        await using var db = CreateDb();
        var html = UofHtml(Row("A", "t", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "蕭維德", "", ""));
        var missing = await Assert.ThrowsAsync<AppException>(() =>
            Service(db).ImportAsync(HtmlFile(html), 1, null, 12, 11));
        Assert.Equal("請選擇處理中、加簽與已結案小分類", missing.Message);
        var same = await Assert.ThrowsAsync<AppException>(() =>
            Service(db).ImportAsync(HtmlFile(html), 1, 10, 12, 10));
        Assert.Equal("處理中、加簽與已結案不可相同", same.Message);
        var unknown = await Assert.ThrowsAsync<AppException>(() =>
            Service(db).ImportAsync(HtmlFile(html), 1, 10, 12, 999));
        Assert.Equal("請選擇處理中、加簽與已結案小分類", unknown.Message);
        db.MajorCategories.Add(new MajorCategory { MajorCategoryId = 2, CategoryName = "預約", ColorHex = "#91BFD5", SortOrder = 2 });
        db.SubCategories.Add(new SubCategory
        {
            SubCategoryId = 20,
            MajorCategoryId = 2,
            SubCategoryName = "已發信",
            ColorHex = "#A9D6E8",
            SortOrder = 1
        });
        await db.SaveChangesAsync();
        var foreign = await Assert.ThrowsAsync<AppException>(() =>
            Service(db).ImportAsync(HtmlFile(html), 1, 20, 12, 11));
        Assert.Equal("小分類不屬於大分類「議題」", foreign.Message);
    }

    [Fact]
    public async Task Import_zero_valid_rows_still_lists_imported_not_found()
    {
        await using var db = CreateDb();
        db.Issues.Add(new IssueItem
        {
            IssueNo = "KEEP",
            Title = "手建不列",
            Content = "",
            MajorCategoryId = 1,
            ClientCompanyId = 1,
            ImportedFromUof = false,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.Issues.Add(new IssueItem
        {
            IssueNo = "UOF",
            Title = "曾匯入",
            Content = "",
            MajorCategoryId = 1,
            ClientCompanyId = 1,
            ImportedFromUof = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        var html = UofHtml(Row("X", "別人的單", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "張楷正", "", ""));
        var result = await Service(db).ImportAsync(HtmlFile(html), 1, 10, 12, 11);
        Assert.Equal(0, result.Summary.Created);
        Assert.Equal(1, result.Summary.Skipped);
        Assert.Single(result.NotFound);
        Assert.Equal("UOF", result.NotFound[0].IssueNo);
    }

    [Fact]
    public async Task Import_rejects_xlsx_extension()
    {
        await using var db = CreateDb();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            Service(db).ImportAsync(HtmlFile("<table></table>", "a.xlsx"), 1, 10, 12, 11));
        Assert.Equal("僅支援 xls 或 html", ex.Message);
    }

    [Fact]
    public async Task Decisions_require_exact_coverage_and_keep()
    {
        await using var db = CreateDb();
        var cache = new ImportNotFoundCache();
        var svc = Service(db, cache);
        var first = await svc.ImportAsync(HtmlFile(UofHtml(
            Row("T1", "一", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "蕭維德", "", ""))), 1, 10, 12, 11);
        Assert.Empty(first.NotFound);
        var second = await svc.ImportAsync(HtmlFile(UofHtml(
            Row("T2", "二", "c", "2026/09/01", "客戶名稱 : 亞家科技", "處理中", "蕭維德", "", ""))), 1, 10, 12, 11);
        var missing = Assert.Single(second.NotFound);
        var empty = await Assert.ThrowsAsync<AppException>(() =>
            svc.ApplyDecisionsAsync(new IssueImportDecisionRequestDto { Decisions = [] }));
        Assert.Equal(400, empty.StatusCode);
        await svc.ApplyDecisionsAsync(new IssueImportDecisionRequestDto
        {
            Decisions = [new IssueImportDecisionItemDto { Id = missing.Id, Action = "keep" }]
        });
        Assert.True(db.Issues.Single(x => x.IssueNo == "T1").MissingKept);
    }

    [Fact]
    public async Task Update_locks_imported_fields()
    {
        await using var db = CreateDb();
        db.Issues.Add(new IssueItem
        {
            IssueNo = "LOCK",
            Title = "標題",
            Content = "內容",
            MajorCategoryId = 1,
            DueDate = new DateOnly(2026, 9, 1),
            ClientCompanyId = 1,
            ImportedFromUof = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        var issues = new IssueService(db);
        var locked = await Assert.ThrowsAsync<AppException>(() => issues.UpdateAsync(1, new IssueWriteDto
        {
            IssueNo = "LOCK",
            Title = "改掉",
            Content = "內容",
            MajorCategoryId = 1,
            DueDate = new DateOnly(2026, 9, 1),
            ClientCompanyId = 1
        }));
        Assert.Equal(400, locked.StatusCode);
        var ok = await issues.UpdateAsync(1, new IssueWriteDto
        {
            IssueNo = "LOCK",
            Title = "標題",
            Content = "內容",
            MajorCategoryId = 1,
            DueDate = new DateOnly(2026, 9, 1),
            ClientCompanyId = 1,
            Remark = "可改"
        });
        Assert.Equal("可改", ok.Remark);
        Assert.True(ok.ImportedFromUof);
    }

    private static IFormFile HtmlFile(string html, string name = "議題.xls")
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/html"
        };
    }

    private static string UofHtml(params string[] rows)
    {
        var header = "<tr>"
            + "<th>申請者</th><th>表單編號</th><th>狀態</th><th>目前簽核者</th>"
            + "<th>&#38928;&#35336;&#23436;&#25104;&#26085;</th>"
            + "<th>SA</th><th>&#24037;&#31243;&#24107;-1</th><th>&#24037;&#31243;&#24107;-2</th>"
            + "<th>&#35696;&#38988;&#27161;&#38988;</th><th>&#35696;&#38988;&#20839;&#23481;</th>"
            + "<th>&#24288;&#21830;&#36039;&#35338;</th></tr>";
        return $"<html><body><table>{header}{string.Join("", rows)}</table></body></html>";
    }

    private static string Row(
        string no, string title, string content, string due, string vendor, string status, string sa, string e1, string e2, string signer = "") =>
        "<tr>"
        + "<td>申請</td>"
        + $"<td>{no}</td><td><span>{status}</span></td><td>{signer}</td><td>{due}</td>"
        + $"<td>{sa}</td><td>{e1}</td><td>{e2}</td>"
        + $"<td>{title}</td><td>{content}</td><td>{vendor}</td></tr>";
}
