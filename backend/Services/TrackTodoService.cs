using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Services;

public class TrackTodoService(AppDbContext db)
{
    public const string TargetMember = "member";
    public const string TargetContact = "clientContact";
    public const string WorkIssue = "issue";
    public const string WorkProject = "project";
    public const string WorkProjectIssue = "projectIssue";
    public const string WorkItem = "workItem";

    public Task<List<TrackTodoDto>> ListForIssueAsync(long issueId) =>
        ListAsync(WorkIssue, issueId, null);

    public Task<List<TrackTodoDto>> CreateForIssueAsync(long issueId, TrackTodoWriteDto input) =>
        CreateAsync(WorkIssue, issueId, null, input);

    public Task<List<TrackTodoDto>> ListForProjectAsync(long projectId) =>
        ListAsync(WorkProject, projectId, null);

    public Task<List<TrackTodoDto>> CreateForProjectAsync(long projectId, TrackTodoWriteDto input) =>
        CreateAsync(WorkProject, projectId, null, input);

    public Task<List<TrackTodoDto>> ListForProjectItemAsync(long projectId, long itemId) =>
        ListAsync(WorkProjectIssue, itemId, projectId);

    public Task<List<TrackTodoDto>> CreateForProjectItemAsync(long projectId, long itemId, TrackTodoWriteDto input) =>
        CreateAsync(WorkProjectIssue, itemId, projectId, input);

    public Task<List<TrackTodoDto>> ListForWorkItemAsync(long projectId, long workItemId) =>
        ListAsync(WorkItem, workItemId, projectId);

    public Task<List<TrackTodoDto>> CreateForWorkItemAsync(long projectId, long workItemId, TrackTodoWriteDto input) =>
        CreateAsync(WorkItem, workItemId, projectId, input);

    public async Task<List<TrackTodoDto>> UpdateAsync(long id, TrackTodoWriteDto input)
    {
        var row = await GetRow(id);
        var scope = await ResolveScopeFromRow(row);
        await ApplyTarget(row, scope.VendorCompanyId, input);
        row.Title = RequireTitle(input.Title);
        row.Content = RequireContent(input.Content);
        row.ReminderDate = input.ReminderDate;
        row.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await ListByScope(scope);
    }

    public async Task<List<TrackTodoDto>> SetCompletedAsync(long id, bool isCompleted)
    {
        var row = await GetRow(id);
        row.IsCompleted = isCompleted;
        row.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await ListByScope(await ResolveScopeFromRow(row));
    }

    public async Task<List<TrackTodoDto>> ReorderAsync(long id, TrackTodoReorderDto input)
    {
        var row = await GetRow(id);
        var scope = await ResolveScopeFromRow(row);
        var siblings = await QueryByScope(scope).ToListAsync();
        var existingIds = siblings.Select(x => x.TrackTodoId).OrderBy(x => x).ToList();
        var orderedIds = input.OrderedIds ?? [];
        var sentIds = orderedIds.OrderBy(x => x).ToList();
        if (existingIds.Count != sentIds.Count || !existingIds.SequenceEqual(sentIds))
        {
            throw new AppException(409, "排序清單必須是該工作下的全部項目");
        }

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var item = siblings.First(x => x.TrackTodoId == orderedIds[i]);
            item.SortOrder = i + 1;
            item.UpdatedAt = DateTime.Now;
        }
        await db.SaveChangesAsync();
        return await ListByScope(scope);
    }

    public async Task<List<TrackTodoDto>> DeleteAsync(long id)
    {
        var row = await GetRow(id);
        var scope = await ResolveScopeFromRow(row);
        db.TrackTodos.Remove(row);
        await db.SaveChangesAsync();
        return await ListByScope(scope);
    }

    public async Task<List<TrackTodoHomeItemDto>> ListHomeAsync()
    {
        var rows = await db.TrackTodos
            .Include(x => x.CompanyMember)
            .Include(x => x.ClientContact)
            .ThenInclude(x => x!.ClientCompany)
            .Include(x => x.Issue)
            .Include(x => x.Project)
            .Include(x => x.ProjectIssue)
            .ThenInclude(x => x!.Project)
            .Include(x => x.ProjectWorkItem)
            .ThenInclude(x => x!.Project)
            .Where(x => !x.IsCompleted)
            .ToListAsync();

        return rows
            .GroupBy(WorkKey)
            .OrderByDescending(g => g.Max(x => x.CreatedAt))
            .SelectMany(g => g.OrderBy(x => x.SortOrder).ThenBy(x => x.TrackTodoId))
            .Select(ToHomeDto)
            .ToList();
    }

    private async Task<List<TrackTodoDto>> ListAsync(string workType, long workId, long? projectId)
    {
        var scope = await ResolveScope(workType, workId, projectId);
        return await ListByScope(scope);
    }

    private async Task<List<TrackTodoDto>> CreateAsync(string workType, long workId, long? projectId, TrackTodoWriteDto input)
    {
        var scope = await ResolveScope(workType, workId, projectId);
        var title = RequireTitle(input.Title);
        var content = RequireContent(input.Content);
        var maxSort = await QueryByScope(scope).Select(x => (int?)x.SortOrder).MaxAsync() ?? 0;
        var now = DateTime.Now;
        var row = new TrackTodo
        {
            IssueId = scope.IssueId,
            ProjectId = scope.OwnProjectId,
            ProjectIssueId = scope.ProjectIssueId,
            ProjectWorkItemId = scope.ProjectWorkItemId,
            Title = title,
            Content = content,
            ReminderDate = input.ReminderDate,
            SortOrder = maxSort + 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        await ApplyTarget(row, scope.VendorCompanyId, input);
        db.TrackTodos.Add(row);
        await db.SaveChangesAsync();
        return await ListByScope(scope);
    }

    private async Task<WorkScope> ResolveScope(string workType, long workId, long? projectId)
    {
        if (workType == WorkIssue)
        {
            var issue = await db.Issues.FirstOrDefaultAsync(x => x.IssueId == workId)
                ?? throw new AppException(404, "找不到該議題");
            return new WorkScope(WorkIssue, workId, issue.IssueId, null, null, null, issue.ClientCompanyId);
        }

        if (workType == WorkProject)
        {
            var project = await db.Projects.FirstOrDefaultAsync(x => x.ProjectId == workId)
                ?? throw new AppException(404, "找不到該專案");
            return new WorkScope(WorkProject, workId, null, project.ProjectId, null, null, project.ClientCompanyId);
        }

        if (workType == WorkItem)
        {
            var workItem = await db.ProjectWorkItems.Include(x => x.Project)
                .FirstOrDefaultAsync(x => x.ProjectWorkItemId == workId)
                ?? throw new AppException(404, "找不到該工作項次");
            if (projectId is long workPid && workItem.ProjectId != workPid)
            {
                throw new AppException(404, "找不到該工作項次");
            }
            return new WorkScope(WorkItem, workItem.ProjectWorkItemId, null, null, null, workItem.ProjectWorkItemId, workItem.Project?.ClientCompanyId);
        }

        var item = await db.ProjectIssues.Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.ProjectIssueId == workId)
            ?? throw new AppException(404, "找不到該專案議題");
        if (projectId is long pid && item.ProjectId != pid)
        {
            throw new AppException(404, "找不到該專案議題");
        }
        return new WorkScope(WorkProjectIssue, item.ProjectIssueId, null, null, item.ProjectIssueId, null, item.Project?.ClientCompanyId);
    }

    private async Task<WorkScope> ResolveScopeFromRow(TrackTodo row)
    {
        if (row.IssueId is long issueId)
        {
            return await ResolveScope(WorkIssue, issueId, null);
        }
        if (row.ProjectId is long projectId)
        {
            return await ResolveScope(WorkProject, projectId, null);
        }
        if (row.ProjectIssueId is long itemId)
        {
            var item = await db.ProjectIssues.FirstOrDefaultAsync(x => x.ProjectIssueId == itemId)
                ?? throw new AppException(404, "找不到該專案議題");
            return await ResolveScope(WorkProjectIssue, itemId, item.ProjectId);
        }
        if (row.ProjectWorkItemId is long workItemId)
        {
            var item = await db.ProjectWorkItems.FirstOrDefaultAsync(x => x.ProjectWorkItemId == workItemId)
                ?? throw new AppException(404, "找不到該工作項次");
            return await ResolveScope(WorkItem, workItemId, item.ProjectId);
        }
        throw new AppException(400, "追蹤 TODO 所屬工作無效");
    }

    private IQueryable<TrackTodo> QueryByScope(WorkScope scope)
    {
        var query = db.TrackTodos.AsQueryable();
        if (scope.IssueId is long issueId)
        {
            return query.Where(x => x.IssueId == issueId);
        }
        if (scope.ProjectIssueId is long itemId)
        {
            return query.Where(x => x.ProjectIssueId == itemId);
        }
        if (scope.ProjectWorkItemId is long workItemId)
        {
            return query.Where(x => x.ProjectWorkItemId == workItemId);
        }
        return query.Where(x => x.ProjectId == scope.OwnProjectId);
    }

    private async Task<List<TrackTodoDto>> ListByScope(WorkScope scope)
    {
        var rows = await QueryByScope(scope)
            .Include(x => x.CompanyMember)
            .Include(x => x.ClientContact)
            .ThenInclude(x => x!.ClientCompany)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.TrackTodoId)
            .ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    private async Task ApplyTarget(TrackTodo row, long? vendorCompanyId, TrackTodoWriteDto input)
    {
        var type = (input.TargetType ?? "").Trim();
        if (type is not (TargetMember or TargetContact))
        {
            throw new AppException(400, "請選擇追蹤對象類型");
        }
        if (input.TargetId is null or <= 0)
        {
            throw new AppException(400, "請選擇追蹤對象");
        }

        var targetId = input.TargetId.Value;
        if (type == TargetMember)
        {
            var member = await db.CompanyMembers.FirstOrDefaultAsync(x => x.CompanyMemberId == targetId);
            if (member is null)
            {
                if (await db.ClientContacts.AnyAsync(x => x.ClientContactId == targetId))
                {
                    throw new AppException(400, "類型與對象不一致");
                }
                throw new AppException(400, "找不到該追蹤對象");
            }
            row.TargetType = TargetMember;
            row.CompanyMemberId = member.CompanyMemberId;
            row.ClientContactId = null;
            return;
        }

        if (vendorCompanyId is null)
        {
            throw new AppException(400, "請先設定廠商");
        }
        var windowCount = await db.ClientContacts.CountAsync(x => x.ClientCompanyId == vendorCompanyId);
        if (windowCount == 0)
        {
            throw new AppException(400, "請先在成員維護新增客戶窗口");
        }
        var contact = await db.ClientContacts.FirstOrDefaultAsync(x => x.ClientContactId == targetId);
        if (contact is null)
        {
            if (await db.CompanyMembers.AnyAsync(x => x.CompanyMemberId == targetId))
            {
                throw new AppException(400, "類型與對象不一致");
            }
            throw new AppException(400, "找不到該追蹤對象");
        }
        if (contact.ClientCompanyId != vendorCompanyId)
        {
            throw new AppException(400, "客戶窗口須屬於該工作的客戶公司");
        }
        row.TargetType = TargetContact;
        row.ClientContactId = contact.ClientContactId;
        row.CompanyMemberId = null;
    }

    private async Task<TrackTodo> GetRow(long id)
    {
        return await db.TrackTodos.FirstOrDefaultAsync(x => x.TrackTodoId == id)
            ?? throw new AppException(404, "找不到該筆需要追蹤的 TODO");
    }

    private static string RequireTitle(string? title)
    {
        var value = title?.Trim() ?? "";
        if (value.Length == 0)
        {
            throw new AppException(400, "標題不可空白");
        }
        if (value.Length > 200)
        {
            throw new AppException(400, "標題過長");
        }
        return value;
    }

    private static string RequireContent(string? content)
    {
        var value = content?.Trim() ?? "";
        if (value.Length > 2000)
        {
            throw new AppException(400, "內容過長");
        }
        return value;
    }

    private static string WorkKey(TrackTodo x) =>
        x.IssueId is long issueId ? $"{WorkIssue}:{issueId}"
        : x.ProjectId is long projectId ? $"{WorkProject}:{projectId}"
        : x.ProjectWorkItemId is long workItemId ? $"{WorkItem}:{workItemId}"
        : $"{WorkProjectIssue}:{x.ProjectIssueId}";

    private static TrackTodoDto ToDto(TrackTodo x)
    {
        var (workType, workId) = WorkOf(x);
        var (targetId, name, secondary) = TargetOf(x);
        return new TrackTodoDto
        {
            Id = x.TrackTodoId,
            WorkType = workType,
            WorkId = workId,
            IsCompleted = x.IsCompleted,
            Title = x.Title,
            Content = x.Content,
            TargetType = x.TargetType,
            TargetId = targetId,
            TargetName = name,
            TargetSecondaryName = secondary,
            ReminderDate = x.ReminderDate,
            SortOrder = x.SortOrder,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };
    }

    private static TrackTodoHomeItemDto ToHomeDto(TrackTodo x)
    {
        var dto = ToDto(x);
        return new TrackTodoHomeItemDto
        {
            Id = dto.Id,
            WorkType = dto.WorkType,
            WorkId = dto.WorkId,
            ProjectId = x.ProjectIssue?.ProjectId ?? x.ProjectWorkItem?.ProjectId,
            Title = dto.Title,
            TargetType = dto.TargetType,
            TargetId = dto.TargetId,
            TargetName = dto.TargetName,
            TargetSecondaryName = dto.TargetSecondaryName,
            WorkLabel = WorkLabel(x),
            ReminderDate = dto.ReminderDate,
            SortOrder = dto.SortOrder,
            CreatedAt = dto.CreatedAt
        };
    }

    private static (string WorkType, long WorkId) WorkOf(TrackTodo x)
    {
        if (x.IssueId is long issueId)
        {
            return (WorkIssue, issueId);
        }
        if (x.ProjectId is long projectId)
        {
            return (WorkProject, projectId);
        }
        if (x.ProjectWorkItemId is long workItemId)
        {
            return (WorkItem, workItemId);
        }
        return (WorkProjectIssue, x.ProjectIssueId ?? 0);
    }

    private static (long Id, string Name, string Secondary) TargetOf(TrackTodo x)
    {
        if (x.TargetType == TargetMember)
        {
            return (x.CompanyMemberId ?? 0, x.CompanyMember?.MemberName ?? "", x.CompanyMember?.EnglishName ?? "");
        }
        return (
            x.ClientContactId ?? 0,
            x.ClientContact?.ContactName ?? "",
            x.ClientContact?.ClientCompany?.CompanyName ?? "");
    }

    private static string WorkLabel(TrackTodo x)
    {
        if (x.Issue is not null)
        {
            return string.IsNullOrWhiteSpace(x.Issue.IssueNo) ? x.Issue.Title : $"{x.Issue.IssueNo} {x.Issue.Title}";
        }
        if (x.Project is not null)
        {
            return $"{x.Project.ProjectCode} {x.Project.ProjectName}";
        }
        if (x.ProjectIssue is not null)
        {
            var code = x.ProjectIssue.Project?.ProjectCode;
            var head = string.IsNullOrWhiteSpace(code) ? x.ProjectIssue.SeqNo : $"{code} {x.ProjectIssue.SeqNo}";
            return $"{head} {x.ProjectIssue.Title}";
        }
        if (x.ProjectWorkItem is not null)
        {
            var code = x.ProjectWorkItem.Project?.ProjectCode;
            var head = string.IsNullOrWhiteSpace(code)
                ? x.ProjectWorkItem.WorkItemCode
                : $"{code} {x.ProjectWorkItem.WorkItemCode}";
            return string.IsNullOrWhiteSpace(x.ProjectWorkItem.Title)
                ? head
                : $"{head} {x.ProjectWorkItem.Title}";
        }
        return x.Title;
    }

    private sealed record WorkScope(
        string WorkType,
        long WorkId,
        long? IssueId,
        long? OwnProjectId,
        long? ProjectIssueId,
        long? ProjectWorkItemId,
        long? VendorCompanyId);
}
