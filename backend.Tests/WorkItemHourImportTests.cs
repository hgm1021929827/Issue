using ClosedXML.Excel;
using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Issue.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Tests;

public class WorkItemHourImportTests
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
            ColorHex = "#8FA8C8",
            SortOrder = 1
        });
        db.ClientCompanies.Add(new ClientCompany
        {
            ClientCompanyId = 1,
            CompanyName = "測試客戶",
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

    private static async Task<ProjectDetailDto> SeedProject(AppDbContext db, string code = "P-1")
    {
        return await new ProjectService(db).CreateAsync(new ProjectWriteDto
        {
            Code = code,
            Name = "測試專案",
            MajorCategoryId = 1,
            ClientCompanyId = 1
        });
    }

    [Theory]
    [InlineData("P-20260108-1497 104-中華資安-UOFX客製.xlsx", "P-20260108-1497", "104-中華資安-UOFX客製")]
    [InlineData("P-ONLY.xlsx", "P-ONLY", "")]
    public void FileName_parses_code_before_first_space(string fileName, string code, string name)
    {
        Assert.True(ProjectImportService.TryParseFileName(fileName, out var parsed, out var suggested));
        Assert.Equal(code, parsed);
        Assert.Equal(name, suggested);
    }

    [Fact]
    public void FileName_empty_stem_fails()
    {
        Assert.False(ProjectImportService.TryParseFileName(".xlsx", out _, out _));
    }

    [Theory]
    [InlineData("All", "蕭維德", true)]
    [InlineData("all", "蕭維德", true)]
    [InlineData("蕭維德、李中凱", "蕭維德", true)]
    [InlineData("李中凱", "蕭維德", false)]
    [InlineData("", "蕭維德", false)]
    public void Owner_filter_all_or_contains_chinese_name(string cell, string name, bool pass)
    {
        Assert.Equal(pass, ProjectImportService.PassesOwnerFilter(cell, name));
    }

    [Theory]
    [InlineData("Eva", "蕭維德", "Eva", true)]
    [InlineData("eva、中華資安", "蕭維德", "Eva", true)]
    [InlineData("Hill", "蕭維德", "Wade", false)]
    [InlineData("Wade、李中凱", "蕭維德", "Wade", true)]
    [InlineData("Eva", "蕭維德", "", false)]
    public void Owner_filter_also_matches_english_name(string cell, string chinese, string english, bool pass)
    {
        Assert.Equal(pass, ProjectImportService.PassesOwnerFilter(cell, chinese, english));
    }

    [Fact]
    public void Remark_parses_date_and_hours()
    {
        var rows = ProjectImportService.ParseRemarkHours("6/18 - 4.5H\n2026/6/12 - 3H\n3/16-2h", 2026);
        Assert.Contains(rows, x => x.Date == new DateOnly(2026, 6, 18) && x.Hours == 4.5m);
        Assert.Contains(rows, x => x.Date == new DateOnly(2026, 6, 12) && x.Hours == 3m);
        Assert.Contains(rows, x => x.Date == new DateOnly(2026, 3, 16) && x.Hours == 2m);
        Assert.False(ProjectImportService.LooksUnparseable("8/7 - 6H\n8/10 - 5.5H\n9/3 - 1.5H\n104 9/3 完成掛選單作業，亞家9/3、9/4 完成調整及確認"));
        Assert.True(ProjectImportService.LooksUnparseable("1/20  表單討論7張  6H"));
        var lines = ProjectImportService.ParseRemarkLines("8/7 - 6H\n8/10 - 5.5H\n9/3 - 1.5H\n104 9/3 完成掛選單作業，亞家9/3、9/4 完成調整及確認", 2026);
        Assert.Equal(4, lines.Count);
        Assert.Equal("hour", lines[0].Kind);
        Assert.Equal(new DateOnly(2026, 8, 7), lines[0].Date);
        Assert.Equal(6m, lines[0].Hours);
        Assert.Equal("hour", lines[1].Kind);
        Assert.Equal("hour", lines[2].Kind);
        Assert.Equal("text", lines[3].Kind);
        Assert.StartsWith("104 9/3", lines[3].Text);
        Assert.True(ProjectImportService.RemarkContainsLine(
            "先前說明\n104 9/3 完成掛選單作業，亞家9/3、9/4 完成調整及確認",
            "104 9/3 完成掛選單作業，亞家9/3、9/4 完成調整及確認"));
        Assert.False(ProjectImportService.RemarkContainsLine("先前說明", "104 9/3 完成掛選單作業，亞家9/3、9/4 完成調整及確認"));
    }

    [Fact]
    public async Task Reimport_skips_text_line_already_in_item_remark()
    {
        await using var db = CreateDb();
        var project = await SeedProject(db, "P-58");
        const string remark = "8/7 - 6H\n8/10 - 5.5H\n9/3 - 1.5H\n104 9/3 完成掛選單作業，亞家9/3、9/4 完成調整及確認";
        var imports = new ProjectImportService(db, new ImportNotFoundCache());
        var first = await imports.ImportAsync(project.Id, ExcelFile("P-58.xlsx", owner: "All", remark: remark, code: "5.8"), 1);
        var review = Assert.Single(first.HourReviews, x => x.OwnerKind == "workItem");
        Assert.Contains(review.Lines, x => x.Kind == "text" && x.Text.StartsWith("104 9/3"));

        var item = db.ProjectWorkItems.Single();
        item.Remark = "104 9/3 完成掛選單作業，亞家9/3、9/4 完成調整及確認";
        await db.SaveChangesAsync();

        var second = await imports.ImportAsync(project.Id, ExcelFile("P-58.xlsx", owner: "All", remark: remark, code: "5.8"), 1);
        Assert.DoesNotContain(second.HourReviews, x => x.OwnerKind == "workItem");
    }

    [Fact]
    public async Task Hours_same_day_is_409_and_complete_without_hours()
    {
        await using var db = CreateDb();
        var project = await SeedProject(db);
        db.ProjectWorkItems.Add(new ProjectWorkItem
        {
            ProjectId = project.Id,
            WorkItemCode = "3.1",
            Title = "項次",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        var id = db.ProjectWorkItems.Select(x => x.ProjectWorkItemId).Single();
        var hours = new WorkHourService(db);
        var items = new WorkItemService(db);
        await hours.CreateForWorkItemAsync(project.Id, id, new WorkHourWriteDto
        {
            Date = new DateOnly(2026, 6, 12),
            Hours = 2
        });
        var ex = await Assert.ThrowsAsync<AppException>(() => hours.CreateForWorkItemAsync(project.Id, id, new WorkHourWriteDto
        {
            Date = new DateOnly(2026, 6, 12),
            Hours = 1
        }));
        Assert.Equal(409, ex.StatusCode);

        var completed = await items.SetCompletedAsync(project.Id, id, true);
        Assert.True(completed.IsCompleted);
        Assert.Equal(new DateOnly(2026, 6, 12), completed.ActualEndDate);
        Assert.Equal(new DateOnly(2026, 6, 12), completed.ActualStartDate);

        db.WorkHours.RemoveRange(db.WorkHours);
        await db.SaveChangesAsync();
        var empty = await items.SetCompletedAsync(project.Id, id, true);
        Assert.True(empty.IsCompleted);
        Assert.Null(empty.ActualEndDate);
    }

    [Fact]
    public async Task Seq_no_cannot_change_after_create()
    {
        await using var db = CreateDb();
        var svc = new ProjectService(db);
        var project = await SeedProject(db);
        var items = await svc.CreateItemAsync(project.Id, new ProjectIssueWriteDto
        {
            SeqNo = "1",
            Title = "議題",
            MajorCategoryId = 1
        });
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.UpdateItemAsync(project.Id, items[0].Id, new ProjectIssueWriteDto
        {
            SeqNo = "2",
            Title = "議題",
            MajorCategoryId = 1
        }));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("項次", ex.Message);
    }

    [Fact]
    public async Task Track_todo_on_work_item_and_delete_project_cascades()
    {
        await using var db = CreateDb();
        var project = await SeedProject(db);
        db.ProjectWorkItems.Add(new ProjectWorkItem
        {
            ProjectId = project.Id,
            WorkItemCode = "2.2.1",
            Title = "說明",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        var workItemId = db.ProjectWorkItems.Select(x => x.ProjectWorkItemId).Single();
        var tracks = new TrackTodoService(db);
        await tracks.CreateForWorkItemAsync(project.Id, workItemId, new TrackTodoWriteDto
        {
            Title = "追項次",
            TargetType = "member",
            TargetId = 1
        });
        var home = await tracks.ListHomeAsync();
        Assert.Contains(home, x => x.WorkType == "workItem" && x.WorkId == workItemId);

        var result = await new ProjectService(db).DeleteAsync(project.Id);
        Assert.Equal(1, result.WorkItemCount);
        Assert.Empty(db.ProjectWorkItems);
        Assert.Empty(db.TrackTodos);
        Assert.Empty(db.WorkHours);
    }

    [Fact]
    public async Task Import_upserts_and_writes_missing_hours()
    {
        await using var db = CreateDb();
        var project = await SeedProject(db, "P-20260108-1497");
        var imports = new ProjectImportService(db, new ImportNotFoundCache());
        var file = ExcelFile("P-20260108-1497 範例.xlsx", owner: "蕭維德、李中凱", remark: "6/12 - 3H\n3/16-2h");
        var result = await imports.ImportAsync(project.Id, file, 1);
        Assert.Equal(1, result.Summary.WorkItemCreated);
        Assert.Equal(1, result.Summary.IssueCreated);
        Assert.Equal(4, db.WorkHours.Count());
        Assert.Contains(db.WorkHours, x => x.WorkDate == new DateOnly(DateTime.Now.Year, 6, 12) && x.HourValue == 3);
        Assert.Contains(db.WorkHours, x => x.WorkDate == new DateOnly(DateTime.Now.Year, 3, 16) && x.HourValue == 2);
        Assert.DoesNotContain(result.HourDiffs, x => x.Kind == "hoursExcelOnly");
        Assert.Equal(new DateOnly(DateTime.Now.Year, 3, 16), db.ProjectWorkItems.Single().ActualStartDate);

        var existing = db.WorkHours.First(x => x.ProjectWorkItemId != null && x.WorkDate.Month == 6);
        existing.HourValue = 9;
        await db.SaveChangesAsync();
        file = ExcelFile("P-20260108-1497 範例.xlsx", owner: "All", title: "更新後", remark: "6/12 - 3H\n3/16-2h");
        result = await imports.ImportAsync(project.Id, file, 1);
        Assert.Equal(1, result.Summary.WorkItemUpdated);
        Assert.Equal("更新後", db.ProjectWorkItems.Single().Title);
        Assert.Equal(9, db.WorkHours.Single(x => x.ProjectWorkItemId != null && x.WorkDate.Month == 6).HourValue);
        Assert.Contains(result.HourDiffs, x => x.Kind == "hoursDiffer" && x.OwnerKind == "workItem");
    }

    [Fact]
    public async Task Import_matches_english_owner_name()
    {
        await using var db = CreateDb();
        var project = await SeedProject(db, "P-EVA");
        var file = ExcelFile("P-EVA.xlsx", owner: "Eva、中華資安", code: "9.1");
        db.CompanyMembers.Single().EnglishName = "Eva";
        await db.SaveChangesAsync();
        var result = await new ProjectImportService(db, new ImportNotFoundCache()).ImportAsync(project.Id, file, 1);
        Assert.Equal(1, result.Summary.WorkItemCreated);
        Assert.Equal("Eva、中華資安", db.ProjectWorkItems.Single().OwnerName);
    }

    [Fact]
    public async Task Import_decisions_keep_or_delete()
    {
        await using var db = CreateDb();
        var project = await SeedProject(db, "P-KEEP");
        db.ProjectWorkItems.Add(new ProjectWorkItem
        {
            ProjectId = project.Id,
            WorkItemCode = "OLD",
            Title = "舊項次",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        var imports = new ProjectImportService(db, new ImportNotFoundCache());
        var file = ExcelFile("P-KEEP 範例.xlsx", owner: "All", code: "NEW");
        var result = await imports.ImportAsync(project.Id, file, 1);
        var missing = Assert.Single(result.NotFound);
        await imports.ApplyDecisionsAsync(project.Id, new ImportDecisionRequestDto
        {
            Decisions =
            [
                new ImportDecisionItemDto { OwnerKind = "workItem", OwnerId = missing.OwnerId, Action = "keep" }
            ]
        });
        Assert.True(db.ProjectWorkItems.Single(x => x.WorkItemCode == "OLD").MissingKept);

        await imports.ApplyDecisionsAsync(project.Id, new ImportDecisionRequestDto
        {
            Decisions =
            [
                new ImportDecisionItemDto { OwnerKind = "workItem", OwnerId = missing.OwnerId, Action = "delete" }
            ]
        });
        Assert.DoesNotContain(db.ProjectWorkItems, x => x.WorkItemCode == "OLD");
    }

    [Fact]
    public async Task Import_decisions_empty_rejected_when_not_found()
    {
        await using var db = CreateDb();
        var project = await SeedProject(db, "P-EMPTY");
        db.ProjectWorkItems.Add(new ProjectWorkItem
        {
            ProjectId = project.Id,
            WorkItemCode = "OLD",
            Title = "舊項次",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        var imports = new ProjectImportService(db, new ImportNotFoundCache());
        var file = ExcelFile("P-EMPTY 範例.xlsx", owner: "All", code: "NEW");
        var result = await imports.ImportAsync(project.Id, file, 1);
        Assert.NotEmpty(result.NotFound);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            imports.ApplyDecisionsAsync(project.Id, new ImportDecisionRequestDto { Decisions = [] }));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("請為每筆未找到資料選擇保留或刪除", ex.Message);
    }

    [Fact]
    public async Task Import_decisions_empty_ok_when_cache_missing()
    {
        await using var db = CreateDb();
        var project = await SeedProject(db, "P-RESTART");
        var imports = new ProjectImportService(db, new ImportNotFoundCache());
        await imports.ApplyDecisionsAsync(project.Id, new ImportDecisionRequestDto { Decisions = [] });
    }

    [Fact]
    public async Task Blank_work_item_code_is_row_error()
    {
        await using var db = CreateDb();
        var project = await SeedProject(db, "P-BLANK");
        using var book = new XLWorkbook();
        var work = book.AddWorksheet("工作項目");
        work.Cell(1, 1).Value = "工作代號";
        work.Cell(1, 2).Value = "工作說明";
        work.Cell(1, 3).Value = "負責人員";
        work.Cell(2, 2).Value = "沒代號";
        work.Cell(2, 3).Value = "All";
        work.Cell(3, 1).Value = "OK";
        work.Cell(3, 2).Value = "有代號";
        work.Cell(3, 3).Value = "All";
        book.AddWorksheet("議題單");
        var file = ToForm(book, "P-BLANK.xlsx");
        var result = await new ProjectImportService(db, new ImportNotFoundCache()).ImportAsync(project.Id, file, 1);
        Assert.Contains(result.RowErrors, x => x.Kind == "missingWorkItemCode");
        Assert.Single(db.ProjectWorkItems);
    }

    private static IFormFile ExcelFile(string name, string owner, string title = "說明", string remark = "", string code = "3.1")
    {
        using var book = new XLWorkbook();
        var work = book.AddWorksheet("工作項目");
        work.Cell(1, 1).Value = "工作代號";
        work.Cell(1, 2).Value = "工作說明";
        work.Cell(1, 3).Value = "計畫人天";
        work.Cell(1, 4).Value = "負責人員";
        work.Cell(1, 5).Value = "預計開始日";
        work.Cell(1, 6).Value = "預計完成日";
        work.Cell(1, 7).Value = "備註";
        work.Cell(2, 1).Value = code;
        work.Cell(2, 2).Value = title;
        work.Cell(2, 3).Value = 1.5;
        work.Cell(2, 4).Value = owner;
        work.Cell(2, 5).Value = new DateTime(2026, 6, 1);
        work.Cell(2, 6).Value = new DateTime(2026, 6, 30);
        work.Cell(2, 7).Value = remark;
        var issue = book.AddWorksheet("議題單");
        issue.Cell(1, 1).Value = "項次";
        issue.Cell(1, 2).Value = "需求說明";
        issue.Cell(1, 3).Value = "解決方案";
        issue.Cell(1, 4).Value = "處理人員";
        issue.Cell(1, 5).Value = "預計完成日";
        issue.Cell(1, 6).Value = "備註";
        issue.Cell(2, 1).Value = "1";
        issue.Cell(2, 2).Value = "需求";
        issue.Cell(2, 3).Value = "作法";
        issue.Cell(2, 4).Value = owner;
        issue.Cell(2, 5).Value = new DateTime(2026, 7, 1);
        issue.Cell(2, 6).Value = remark;
        return ToForm(book, name);
    }

    private static IFormFile ToForm(XLWorkbook book, string name)
    {
        var stream = new MemoryStream();
        book.SaveAs(stream);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }
}
