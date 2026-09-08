using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Services;

public class WorkItemService(AppDbContext db)
{
    public async Task<List<WorkItemDto>> ListAsync(long projectId)
    {
        await EnsureProject(projectId);
        var rows = await db.ProjectWorkItems
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.WorkItemCode)
            .ThenBy(x => x.ProjectWorkItemId)
            .ToListAsync();
        await MarkCompletedWhenActualEndExists(rows);
        var totals = await HoursByWorkItem(rows.Select(x => x.ProjectWorkItemId).ToList());
        return rows.Select(x => ToDto(x, totals.GetValueOrDefault(x.ProjectWorkItemId))).ToList();
    }

    public async Task<WorkItemDto> GetAsync(long projectId, long id)
    {
        var row = await Load(projectId, id);
        await MarkCompletedWhenActualEndExists([row]);
        var total = await db.WorkHours.Where(x => x.ProjectWorkItemId == id).SumAsync(x => (decimal?)x.HourValue) ?? 0;
        return ToDto(row, total);
    }

    public async Task<WorkItemDto> SetCompletedAsync(long projectId, long id, bool isCompleted)
    {
        var row = await Load(projectId, id);
        row.IsCompleted = isCompleted;
        if (isCompleted)
        {
            var last = await db.WorkHours.Where(x => x.ProjectWorkItemId == id)
                .Select(x => (DateOnly?)x.WorkDate)
                .MaxAsync();
            row.ActualEndDate = last;
        }
        else
        {
            row.ActualEndDate = null;
        }
        row.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await GetAsync(projectId, id);
    }

    public async Task<WorkItemDto> UpdateRemarkAsync(long projectId, long id, string? remark)
    {
        var row = await Load(projectId, id);
        var text = (remark ?? "").Trim();
        if (text.Length > 2000)
        {
            throw new AppException(400, "備註過長");
        }
        row.Remark = text;
        row.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await GetAsync(projectId, id);
    }

    public async Task DeleteAsync(long projectId, long id)
    {
        var row = await Load(projectId, id);
        db.TrackTodos.RemoveRange(db.TrackTodos.Where(x => x.ProjectWorkItemId == id));
        db.WorkHours.RemoveRange(db.WorkHours.Where(x => x.ProjectWorkItemId == id));
        db.ProjectWorkItems.Remove(row);
        await db.SaveChangesAsync();
    }

    private async Task<ProjectWorkItem> Load(long projectId, long id)
    {
        await EnsureProject(projectId);
        return await db.ProjectWorkItems.FirstOrDefaultAsync(x => x.ProjectWorkItemId == id && x.ProjectId == projectId)
            ?? throw new AppException(404, "找不到該工作項次");
    }

    private async Task EnsureProject(long projectId)
    {
        if (!await db.Projects.AnyAsync(x => x.ProjectId == projectId))
        {
            throw new AppException(404, "找不到該專案");
        }
    }

    private async Task MarkCompletedWhenActualEndExists(IReadOnlyCollection<ProjectWorkItem> rows)
    {
        var stale = rows.Where(x => x.ActualEndDate != null && !x.IsCompleted).ToList();
        if (stale.Count == 0)
        {
            return;
        }
        foreach (var row in stale)
        {
            row.IsCompleted = true;
            row.UpdatedAt = DateTime.Now;
        }
        await db.SaveChangesAsync();
    }

    private async Task<Dictionary<long, decimal>> HoursByWorkItem(List<long> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }
        return await db.WorkHours
            .Where(x => x.ProjectWorkItemId != null && ids.Contains(x.ProjectWorkItemId.Value))
            .GroupBy(x => x.ProjectWorkItemId!.Value)
            .Select(g => new { g.Key, Total = g.Sum(x => x.HourValue) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
    }

    public static WorkItemDto ToDto(ProjectWorkItem x, decimal hoursTotal) => new()
    {
        Id = x.ProjectWorkItemId,
        ProjectId = x.ProjectId,
        WorkItemCode = x.WorkItemCode,
        Title = x.Title,
        PlannedDays = x.PlannedDays,
        OwnerName = x.OwnerName,
        StartDate = x.StartDate,
        DueDate = x.DueDate,
        IsCompleted = x.IsCompleted,
        ActualStartDate = x.ActualStartDate,
        ActualEndDate = x.ActualEndDate,
        MissingKept = x.MissingKept,
        Remark = x.Remark,
        HoursTotal = hoursTotal,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };
}
