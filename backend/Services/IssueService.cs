using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Services;

public class IssueService(AppDbContext db)
{
    public async Task<List<IssueDto>> ListAsync(
        long? majorCategoryId,
        long? subCategoryId = null,
        long? clientCompanyId = null,
        string? q = null)
    {
        var query = db.Issues.Include(x => x.MajorCategory).Include(x => x.SubCategory).Include(x => x.ClientCompany).AsQueryable();
        if (majorCategoryId is not null)
        {
            query = query.Where(x => x.MajorCategoryId == majorCategoryId);
        }
        if (subCategoryId is not null)
        {
            query = query.Where(x => x.SubCategoryId == subCategoryId);
        }
        if (clientCompanyId is not null)
        {
            query = query.Where(x => x.ClientCompanyId == clientCompanyId);
        }
        var key = (q ?? "").Trim();
        if (key.Length > 0)
        {
            var lower = key.ToLower();
            query = query.Where(x =>
                x.IssueNo.ToLower().Contains(lower)
                || x.Title.ToLower().Contains(lower));
        }
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<IssueDto> GetAsync(long id)
    {
        var item = await LoadAsync(id);
        return ToDto(item);
    }

    public async Task<IssueDto> CreateAsync(IssueWriteDto input)
    {
        await ValidateWrite(input);
        var issueNo = await RequireUniqueIssueNo(input.IssueNo, null);
        var now = DateTime.Now;
        var item = new IssueItem
        {
            IssueNo = issueNo,
            Title = RequireTitle(input.Title),
            Content = input.Content?.Trim() ?? "",
            MajorCategoryId = input.MajorCategoryId,
            SubCategoryId = input.SubCategoryId,
            DueDate = input.DueDate,
            Remark = string.IsNullOrWhiteSpace(input.Remark) ? null : input.Remark.Trim(),
            ClientCompanyId = await RequireCompany(input.ClientCompanyId),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Issues.Add(item);
        await db.SaveChangesAsync();
        return await GetAsync(item.IssueId);
    }

    public async Task<IssueDto> UpdateAsync(long id, IssueWriteDto input)
    {
        var item = await db.Issues.FirstOrDefaultAsync(x => x.IssueId == id)
            ?? throw new AppException(404, "找不到該議題");
        if (item.ImportedFromUof)
        {
            var issueNo = (input.IssueNo ?? "").Trim();
            var title = (input.Title ?? "").Trim();
            var content = input.Content?.Trim() ?? "";
            if (!string.Equals(issueNo, item.IssueNo, StringComparison.OrdinalIgnoreCase)
                || title != item.Title
                || content != (item.Content ?? "")
                || input.DueDate != item.DueDate
                || input.ClientCompanyId != item.ClientCompanyId)
            {
                throw new AppException(400, "此議題由匯入產生，請重新匯入以更新編號、標題、內容、預計完成日或廠商");
            }
        }
        await ValidateWrite(input);
        item.IssueNo = await RequireUniqueIssueNo(input.IssueNo, id);
        item.Title = RequireTitle(input.Title);
        item.Content = input.Content?.Trim() ?? "";
        item.MajorCategoryId = input.MajorCategoryId;
        item.SubCategoryId = input.SubCategoryId;
        item.DueDate = input.DueDate;
        item.Remark = string.IsNullOrWhiteSpace(input.Remark) ? null : input.Remark.Trim();
        var companyId = await RequireCompany(input.ClientCompanyId);
        if (item.ClientCompanyId != companyId)
        {
            var n = await db.TrackTodos.CountAsync(x => x.IssueId == id && x.TargetType == TrackTodoService.TargetContact);
            if (n > 0)
            {
                throw new AppException(409, $"無法更改廠商，尚有 {n} 筆需要追蹤的 TODO 使用客戶窗口");
            }
        }
        item.ClientCompanyId = companyId;
        item.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task DeleteAsync(long id)
    {
        var item = await db.Issues.FirstOrDefaultAsync(x => x.IssueId == id)
            ?? throw new AppException(404, "找不到該議題");
        db.TrackTodos.RemoveRange(db.TrackTodos.Where(x => x.IssueId == id));
        var todos = db.IssueTodos.Where(x => x.IssueId == id);
        db.IssueTodos.RemoveRange(todos);
        db.IssuePlans.RemoveRange(db.IssuePlans.Where(x => x.IssueId == id));
        db.Issues.Remove(item);
        await db.SaveChangesAsync();
    }

    public async Task<List<IssueCalendarItemDto>> CalendarAsync(int year, int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
        {
            throw new AppException(400, "年月無效");
        }
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var dueRows = await db.Issues.Include(x => x.SubCategory)
            .Where(x => x.DueDate != null && x.DueDate >= start && x.DueDate < end)
            .OrderBy(x => x.DueDate)
            .ToListAsync();
        var planRows = await db.IssuePlans.Include(x => x.Issue).ThenInclude(x => x!.SubCategory)
            .Where(x => x.PlanDate >= start && x.PlanDate < end)
            .OrderBy(x => x.PlanDate)
            .ToListAsync();

        var items = dueRows.Select(x => ToCalendarItem(x, x.DueDate!.Value, "due", null)).ToList();
        items.AddRange(planRows
            .Where(x => x.Issue is not null)
            .Select(x => ToCalendarItem(x.Issue!, x.PlanDate, "plan", x.PlanId)));

        var projectRows = await db.Projects.Include(x => x.SubCategory)
            .Where(x => x.DueDate != null && x.DueDate >= start && x.DueDate < end)
            .OrderBy(x => x.DueDate)
            .ToListAsync();
        items.AddRange(projectRows.Select(ToProjectCalendarItem));

        var projectIssueRows = await db.ProjectIssues.Include(x => x.SubCategory).Include(x => x.Project)
            .Where(x => x.DueDate != null && x.DueDate >= start && x.DueDate < end)
            .OrderBy(x => x.DueDate)
            .ToListAsync();
        items.AddRange(projectIssueRows.Select(ToProjectIssueCalendarItem));

        var workItemRows = await db.ProjectWorkItems.Include(x => x.Project).ThenInclude(x => x!.SubCategory)
            .Where(x => x.DueDate != null && x.DueDate >= start && x.DueDate < end)
            .OrderBy(x => x.DueDate)
            .ToListAsync();
        items.AddRange(workItemRows.Select(ToWorkItemCalendarItem));

        var trackRows = await db.TrackTodos
            .Include(x => x.Issue)
            .Include(x => x.Project)
            .Include(x => x.ProjectIssue).ThenInclude(x => x!.Project)
            .Include(x => x.ProjectWorkItem).ThenInclude(x => x!.Project)
            .Include(x => x.CompanyMember)
            .Include(x => x.ClientContact)
            .Where(x => !x.IsCompleted && x.ReminderDate != null && x.ReminderDate >= start && x.ReminderDate < end)
            .OrderBy(x => x.ReminderDate)
            .ThenBy(x => x.SortOrder)
            .ToListAsync();
        items.AddRange(trackRows.Select(ToTrackCalendarItem));
        return items;
    }

    public Task AddPlanAsync(long issueId, DateOnly date) => AddPlansAsync(issueId, [date]);

    public async Task<int> AddPlansAsync(long issueId, IEnumerable<DateOnly> dates)
    {
        var unique = dates.Distinct().OrderBy(x => x).ToList();
        if (unique.Count == 0)
        {
            throw new AppException(400, "請提供日期");
        }
        if (unique.Count > 62)
        {
            throw new AppException(400, "一次最多加入 62 天");
        }
        if (unique.Any(d => d.Year < 2000 || d.Year > 2100))
        {
            throw new AppException(400, "日期無效");
        }
        if (!await db.Issues.AnyAsync(x => x.IssueId == issueId))
        {
            throw new AppException(404, "找不到該議題");
        }

        var existing = await db.IssuePlans
            .Where(x => x.IssueId == issueId && unique.Contains(x.PlanDate))
            .Select(x => x.PlanDate)
            .ToListAsync();
        var toAdd = unique.Except(existing).ToList();
        if (toAdd.Count == 0)
        {
            throw new AppException(409, unique.Count == 1 ? "這天已排入此議題" : "所選日期皆已排入");
        }

        foreach (var date in toAdd)
        {
            db.IssuePlans.Add(new IssuePlan { IssueId = issueId, PlanDate = date });
        }
        await db.SaveChangesAsync();
        return toAdd.Count;
    }

    public Task<int> AddPlansAsync(long issueId, IssuePlanWriteDto input) =>
        AddPlansAsync(issueId, ExpandDates(input));

    private static List<DateOnly> ExpandDates(IssuePlanWriteDto input)
    {
        if (input.Dates is { Count: > 0 })
        {
            return input.Dates;
        }
        if (input.Date is DateOnly start)
        {
            var end = input.EndDate ?? start;
            if (end < start)
            {
                (start, end) = (end, start);
            }
            var span = end.DayNumber - start.DayNumber + 1;
            if (span > 62)
            {
                throw new AppException(400, "一次最多加入 62 天");
            }
            return Enumerable.Range(0, span).Select(i => start.AddDays(i)).ToList();
        }
        throw new AppException(400, "請提供日期");
    }

    public async Task RemovePlanAsync(long issueId, DateOnly date)
    {
        var item = await db.IssuePlans.FirstOrDefaultAsync(x => x.IssueId == issueId && x.PlanDate == date)
            ?? throw new AppException(404, "找不到這天的預計項目");
        db.IssuePlans.Remove(item);
        await db.SaveChangesAsync();
    }

    private async Task<IssueItem> LoadAsync(long id)
    {
        return await db.Issues.Include(x => x.MajorCategory).Include(x => x.SubCategory).Include(x => x.ClientCompany)
            .FirstOrDefaultAsync(x => x.IssueId == id)
            ?? throw new AppException(404, "找不到該議題");
    }

    private async Task ValidateWrite(IssueWriteDto input)
    {
        if (!await db.MajorCategories.AnyAsync(x => x.MajorCategoryId == input.MajorCategoryId))
        {
            throw new AppException(400, "無效的大分類");
        }
        if (input.SubCategoryId is long subId)
        {
            var sub = await db.SubCategories.FirstOrDefaultAsync(x => x.SubCategoryId == subId)
                ?? throw new AppException(400, "無效的小分類");
            if (sub.MajorCategoryId != input.MajorCategoryId)
            {
                throw new AppException(400, "小分類不屬於所選大分類");
            }
        }
    }

    private async Task<long> RequireCompany(long? companyId)
    {
        if (companyId is not long id || id <= 0)
        {
            throw new AppException(400, "請選擇客戶公司");
        }
        if (!await db.ClientCompanies.AnyAsync(x => x.ClientCompanyId == id))
        {
            throw new AppException(400, "無效的客戶公司");
        }
        return id;
    }

    private async Task<string> RequireUniqueIssueNo(string? value, long? excludeIssueId)
    {
        var no = (value ?? "").Trim();
        if (no.Length == 0)
        {
            throw new AppException(400, "編號不可空白");
        }
        if (no.Length > 50)
        {
            throw new AppException(400, "編號過長");
        }
        var taken = await db.Issues.AnyAsync(x => x.IssueNo == no && (excludeIssueId == null || x.IssueId != excludeIssueId));
        if (taken)
        {
            throw new AppException(409, "編號已存在");
        }
        return no;
    }

    private static string RequireTitle(string? title)
    {
        var trimmed = (title ?? "").Trim();
        if (trimmed.Length == 0)
        {
            throw new AppException(400, "標題不可空白");
        }
        if (trimmed.Length > 200)
        {
            throw new AppException(400, "標題過長");
        }
        return trimmed;
    }

    private static string DisplayColor(IssueItem x)
    {
        var hex = x.SubCategory?.ColorHex;
        return ColorPresets.IsAllowed(hex) ? ColorPresets.Normalize(hex!) : "#8FA8C8";
    }

    private static IssueCalendarItemDto ToCalendarItem(IssueItem x, DateOnly date, string kind, long? planId)
    {
        var color = DisplayColor(x);
        return new IssueCalendarItemDto
        {
            Id = x.IssueId,
            PlanId = planId,
            ProjectId = null,
            Source = "issue",
            Kind = kind,
            Label = x.IssueNo,
            IssueNo = x.IssueNo,
            Title = x.Title,
            Content = x.Content ?? "",
            Date = date,
            DueDate = date,
            MajorCategoryId = x.MajorCategoryId,
            MajorCategoryColor = color,
            SubCategoryColor = color
        };
    }

    private static IssueCalendarItemDto ToProjectCalendarItem(Project x)
    {
        var date = x.DueDate!.Value;
        var color = DisplayColorHex(x.SubCategory?.ColorHex);
        return new IssueCalendarItemDto
        {
            Id = x.ProjectId,
            ProjectId = x.ProjectId,
            Source = "project",
            Kind = "due",
            Label = x.ProjectCode,
            IssueNo = x.ProjectCode,
            Title = x.ProjectName,
            Content = string.IsNullOrWhiteSpace(x.ProjectName) ? x.Description : x.ProjectName,
            Date = date,
            DueDate = date,
            MajorCategoryId = x.MajorCategoryId,
            MajorCategoryColor = color,
            SubCategoryColor = color
        };
    }

    private static IssueCalendarItemDto ToProjectIssueCalendarItem(ProjectIssue x)
    {
        var date = x.DueDate!.Value;
        var color = DisplayColorHex(x.SubCategory?.ColorHex);
        var code = x.Project?.ProjectCode ?? "";
        var label = $"{code} {x.SeqNo}".Trim();
        return new IssueCalendarItemDto
        {
            Id = x.ProjectIssueId,
            ProjectId = x.ProjectId,
            Source = "projectIssue",
            Kind = "due",
            Label = label,
            IssueNo = label,
            Title = x.Title,
            Content = x.Title,
            Date = date,
            DueDate = date,
            MajorCategoryId = x.MajorCategoryId,
            MajorCategoryColor = color,
            SubCategoryColor = color
        };
    }

    private static IssueCalendarItemDto ToWorkItemCalendarItem(ProjectWorkItem x)
    {
        var date = x.DueDate!.Value;
        var color = DisplayColorHex(x.Project?.SubCategory?.ColorHex);
        var code = x.Project?.ProjectCode ?? "";
        var label = $"{code} {x.WorkItemCode}".Trim();
        return new IssueCalendarItemDto
        {
            Id = x.ProjectWorkItemId,
            ProjectId = x.ProjectId,
            Source = "workItem",
            Kind = "due",
            Label = label,
            IssueNo = label,
            Title = x.Title,
            Content = x.Title,
            Date = date,
            DueDate = date,
            MajorCategoryId = x.Project?.MajorCategoryId ?? 0,
            MajorCategoryColor = color,
            SubCategoryColor = color
        };
    }

    private static IssueCalendarItemDto ToTrackCalendarItem(TrackTodo x)
    {
        var date = x.ReminderDate!.Value;
        var (workType, workId) = x.IssueId is long issueId
            ? ("issue", issueId)
            : x.ProjectId is long projectId
                ? ("project", projectId)
                : x.ProjectWorkItemId is long workItemId
                    ? ("workItem", workItemId)
                    : ("projectIssue", x.ProjectIssueId ?? 0);
        var projectIdForJump = x.ProjectIssue?.ProjectId ?? x.ProjectWorkItem?.ProjectId;
        var label = TrackCalendarLabel(x);
        var target = x.TargetType == TrackTodoService.TargetMember
            ? $"{x.CompanyMember?.MemberName}（{x.CompanyMember?.EnglishName}）"
            : x.ClientContact?.ContactName ?? "";
        return new IssueCalendarItemDto
        {
            Id = x.TrackTodoId,
            ProjectId = projectIdForJump,
            Source = "trackTodo",
            Kind = "track",
            WorkType = workType,
            WorkId = workId,
            Label = label,
            IssueNo = label,
            Title = x.Title,
            Content = string.IsNullOrWhiteSpace(target) ? x.Title : target,
            Date = date,
            DueDate = date,
            MajorCategoryId = x.Issue?.MajorCategoryId ?? x.Project?.MajorCategoryId ?? x.ProjectIssue?.MajorCategoryId ?? x.ProjectWorkItem?.Project?.MajorCategoryId ?? 0,
            MajorCategoryColor = "#B7D0E0",
            SubCategoryColor = "#B7D0E0"
        };
    }

    private static string TrackCalendarLabel(TrackTodo x)
    {
        if (x.Issue is not null)
        {
            return string.IsNullOrWhiteSpace(x.Issue.IssueNo)
                ? x.Issue.Title
                : $"{x.Issue.IssueNo} {x.Issue.Title}".Trim();
        }
        if (x.Project is not null)
        {
            return string.IsNullOrWhiteSpace(x.Project.ProjectName)
                ? x.Project.ProjectCode
                : $"{x.Project.ProjectCode} {x.Project.ProjectName}".Trim();
        }
        if (x.ProjectIssue is not null)
        {
            var code = x.ProjectIssue.Project?.ProjectCode;
            var head = string.IsNullOrWhiteSpace(code) ? x.ProjectIssue.SeqNo : $"{code} {x.ProjectIssue.SeqNo}";
            return string.IsNullOrWhiteSpace(x.ProjectIssue.Title) ? head : $"{head} {x.ProjectIssue.Title}".Trim();
        }
        if (x.ProjectWorkItem is not null)
        {
            var code = x.ProjectWorkItem.Project?.ProjectCode;
            var head = string.IsNullOrWhiteSpace(code)
                ? x.ProjectWorkItem.WorkItemCode
                : $"{code} {x.ProjectWorkItem.WorkItemCode}";
            return string.IsNullOrWhiteSpace(x.ProjectWorkItem.Title) ? head : $"{head} {x.ProjectWorkItem.Title}".Trim();
        }
        return x.Title;
    }

    private static string DisplayColorHex(string? hex) =>
        ColorPresets.IsAllowed(hex) ? ColorPresets.Normalize(hex!) : "#8FA8C8";

    private static IssueDto ToDto(IssueItem x) => new()
    {
        Id = x.IssueId,
        IssueNo = x.IssueNo,
        Title = x.Title,
        Content = x.Content,
        MajorCategoryId = x.MajorCategoryId,
        MajorCategoryName = x.MajorCategory?.CategoryName ?? "",
        MajorCategoryColor = DisplayColor(x),
        SubCategoryId = x.SubCategoryId,
        SubCategoryName = x.SubCategory?.SubCategoryName,
        SubCategoryColor = DisplayColor(x),
        DueDate = x.DueDate,
        Remark = x.Remark,
        ClientCompanyId = x.ClientCompanyId,
        ClientCompanyName = x.ClientCompany?.CompanyName,
        ImportedFromUof = x.ImportedFromUof,
        MissingKept = x.MissingKept,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };
}
