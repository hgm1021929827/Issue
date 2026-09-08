using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Services;

public class WorkHourService(AppDbContext db)
{
    public const string OwnerWorkItem = "workItem";
    public const string OwnerIssue = "projectIssue";

    public Task<List<WorkHourDto>> ListForWorkItemAsync(long projectId, long workItemId) =>
        ListAsync(projectId, OwnerWorkItem, workItemId);

    public Task<List<WorkHourDto>> CreateForWorkItemAsync(long projectId, long workItemId, WorkHourWriteDto input) =>
        CreateAsync(projectId, OwnerWorkItem, workItemId, input);

    public Task<List<WorkHourDto>> UpdateForWorkItemAsync(long projectId, long workItemId, long hourId, WorkHourWriteDto input) =>
        UpdateAsync(projectId, OwnerWorkItem, workItemId, hourId, input);

    public Task<List<WorkHourDto>> DeleteForWorkItemAsync(long projectId, long workItemId, long hourId) =>
        DeleteAsync(projectId, OwnerWorkItem, workItemId, hourId);

    public Task<List<WorkHourDto>> ListForIssueAsync(long projectId, long itemId) =>
        ListAsync(projectId, OwnerIssue, itemId);

    public Task<List<WorkHourDto>> CreateForIssueAsync(long projectId, long itemId, WorkHourWriteDto input) =>
        CreateAsync(projectId, OwnerIssue, itemId, input);

    public Task<List<WorkHourDto>> UpdateForIssueAsync(long projectId, long itemId, long hourId, WorkHourWriteDto input) =>
        UpdateAsync(projectId, OwnerIssue, itemId, hourId, input);

    public Task<List<WorkHourDto>> DeleteForIssueAsync(long projectId, long itemId, long hourId) =>
        DeleteAsync(projectId, OwnerIssue, itemId, hourId);

    public async Task RecalcActualStartAsync(string ownerKind, long ownerId)
    {
        var dates = await QueryByOwner(ownerKind, ownerId).Select(x => x.WorkDate).ToListAsync();
        var start = dates.Count == 0 ? (DateOnly?)null : dates.Min();
        if (ownerKind == OwnerWorkItem)
        {
            var row = await db.ProjectWorkItems.FirstOrDefaultAsync(x => x.ProjectWorkItemId == ownerId)
                ?? throw new AppException(404, "找不到該工作項次");
            row.ActualStartDate = start;
            row.UpdatedAt = DateTime.Now;
        }
        else
        {
            var row = await db.ProjectIssues.FirstOrDefaultAsync(x => x.ProjectIssueId == ownerId)
                ?? throw new AppException(404, "找不到該專案議題");
            row.ActualStartDate = start;
            row.UpdatedAt = DateTime.Now;
        }
        await db.SaveChangesAsync();
    }

    private async Task<List<WorkHourDto>> ListAsync(long projectId, string ownerKind, long ownerId)
    {
        await EnsureOwner(projectId, ownerKind, ownerId);
        return await QueryByOwner(ownerKind, ownerId)
            .OrderBy(x => x.WorkDate)
            .ThenBy(x => x.WorkHourId)
            .Select(x => ToDto(x))
            .ToListAsync();
    }

    private async Task<List<WorkHourDto>> CreateAsync(long projectId, string ownerKind, long ownerId, WorkHourWriteDto input)
    {
        await EnsureOwner(projectId, ownerKind, ownerId);
        var date = RequireDate(input.Date);
        var hours = RequireHours(input.Hours);
        var remark = RequireRemark(input.Remark);
        if (await QueryByOwner(ownerKind, ownerId).AnyAsync(x => x.WorkDate == date))
        {
            throw new AppException(409, "同一日已有工時");
        }

        db.WorkHours.Add(new WorkHour
        {
            ProjectWorkItemId = ownerKind == OwnerWorkItem ? ownerId : null,
            ProjectIssueId = ownerKind == OwnerIssue ? ownerId : null,
            WorkDate = date,
            HourValue = hours,
            Remark = remark
        });
        await db.SaveChangesAsync();
        await RecalcActualStartAsync(ownerKind, ownerId);
        return await ListAsync(projectId, ownerKind, ownerId);
    }

    private async Task<List<WorkHourDto>> UpdateAsync(long projectId, string ownerKind, long ownerId, long hourId, WorkHourWriteDto input)
    {
        await EnsureOwner(projectId, ownerKind, ownerId);
        var row = await QueryByOwner(ownerKind, ownerId).FirstOrDefaultAsync(x => x.WorkHourId == hourId)
            ?? throw new AppException(404, "找不到該筆工時");
        var date = RequireDate(input.Date);
        if (await QueryByOwner(ownerKind, ownerId).AnyAsync(x => x.WorkDate == date && x.WorkHourId != hourId))
        {
            throw new AppException(409, "同一日已有工時");
        }
        row.WorkDate = date;
        row.HourValue = RequireHours(input.Hours);
        row.Remark = RequireRemark(input.Remark);
        await db.SaveChangesAsync();
        await RecalcActualStartAsync(ownerKind, ownerId);
        return await ListAsync(projectId, ownerKind, ownerId);
    }

    private async Task<List<WorkHourDto>> DeleteAsync(long projectId, string ownerKind, long ownerId, long hourId)
    {
        await EnsureOwner(projectId, ownerKind, ownerId);
        var row = await QueryByOwner(ownerKind, ownerId).FirstOrDefaultAsync(x => x.WorkHourId == hourId)
            ?? throw new AppException(404, "找不到該筆工時");
        db.WorkHours.Remove(row);
        await db.SaveChangesAsync();
        await RecalcActualStartAsync(ownerKind, ownerId);
        return await ListAsync(projectId, ownerKind, ownerId);
    }

    private async Task EnsureOwner(long projectId, string ownerKind, long ownerId)
    {
        if (!await db.Projects.AnyAsync(x => x.ProjectId == projectId))
        {
            throw new AppException(404, "找不到該專案");
        }
        if (ownerKind == OwnerWorkItem)
        {
            if (!await db.ProjectWorkItems.AnyAsync(x => x.ProjectWorkItemId == ownerId && x.ProjectId == projectId))
            {
                throw new AppException(404, "找不到該工作項次");
            }
            return;
        }
        if (!await db.ProjectIssues.AnyAsync(x => x.ProjectIssueId == ownerId && x.ProjectId == projectId))
        {
            throw new AppException(404, "找不到該專案議題");
        }
    }

    private IQueryable<WorkHour> QueryByOwner(string ownerKind, long ownerId) =>
        ownerKind == OwnerWorkItem
            ? db.WorkHours.Where(x => x.ProjectWorkItemId == ownerId)
            : db.WorkHours.Where(x => x.ProjectIssueId == ownerId);

    private static DateOnly RequireDate(DateOnly date)
    {
        if (date.Year < 2000 || date.Year > 2100)
        {
            throw new AppException(400, "日期無效");
        }
        return date;
    }

    private static decimal RequireHours(decimal hours)
    {
        if (hours <= 0 || hours > 999.99m)
        {
            throw new AppException(400, "工時須大於 0 且不可超過 999.99");
        }
        if (decimal.Round(hours, 2) != hours)
        {
            throw new AppException(400, "工時最多兩位小數");
        }
        return hours;
    }

    private static string RequireRemark(string? remark)
    {
        var value = remark?.Trim() ?? "";
        if (value.Length > 2000)
        {
            throw new AppException(400, "備註過長");
        }
        return value;
    }

    private static WorkHourDto ToDto(WorkHour x) => new()
    {
        Id = x.WorkHourId,
        Date = x.WorkDate,
        Hours = x.HourValue,
        Remark = x.Remark
    };
}
