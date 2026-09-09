using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Services;

public class ProjectService(AppDbContext db)
{
    public async Task<List<ProjectListItemDto>> ListAsync(long? majorCategoryId)
    {
        var query = db.Projects
            .Include(x => x.MajorCategory)
            .Include(x => x.SubCategory)
            .Include(x => x.ClientCompany)
            .Include(x => x.OwnerMember)
            .AsQueryable();
        if (majorCategoryId is not null)
        {
            query = query.Where(x => x.MajorCategoryId == majorCategoryId);
        }
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.ProjectId).ToListAsync();
        var counts = await db.ProjectIssues.GroupBy(x => x.ProjectId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var workCounts = await db.ProjectWorkItems.GroupBy(x => x.ProjectId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        return rows.Select(x => ToListItem(x, counts.GetValueOrDefault(x.ProjectId), workCounts.GetValueOrDefault(x.ProjectId))).ToList();
    }

    public async Task<ProjectDetailDto> GetAsync(long id)
    {
        var item = await LoadProjectAsync(id);
        var issues = await ListItemsAsync(id);
        var workItemCount = await db.ProjectWorkItems.CountAsync(x => x.ProjectId == id);
        var dto = ToListItem(item, issues.Count, workItemCount);
        return new ProjectDetailDto
        {
            Id = dto.Id,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            MajorCategoryId = dto.MajorCategoryId,
            MajorCategoryName = dto.MajorCategoryName,
            MajorCategoryColor = dto.MajorCategoryColor,
            SubCategoryId = dto.SubCategoryId,
            SubCategoryName = dto.SubCategoryName,
            SubCategoryColor = dto.SubCategoryColor,
            StartDate = dto.StartDate,
            DueDate = dto.DueDate,
            ItemCount = dto.ItemCount,
            WorkItemCount = dto.WorkItemCount,
            ClientCompanyId = dto.ClientCompanyId,
            ClientCompanyName = dto.ClientCompanyName,
            OwnerMemberId = dto.OwnerMemberId,
            OwnerMemberName = dto.OwnerMemberName,
            OwnerMemberEnglishName = dto.OwnerMemberEnglishName,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            Items = issues
        };
    }

    public async Task<ProjectDetailDto> CreateAsync(ProjectWriteDto input)
    {
        await ValidateCategoryAndDates(input.MajorCategoryId, input.SubCategoryId, input.StartDate, input.DueDate);
        var companyId = await RequireCompany(input.ClientCompanyId);
        var ownerId = await OptionalMember(input.OwnerMemberId);
        var now = DateTime.Now;
        var item = new Project
        {
            ProjectCode = await RequireUniqueCode(input.Code, null),
            ProjectName = RequireName(input.Name),
            Description = TrimText(input.Description, 4000),
            MajorCategoryId = input.MajorCategoryId,
            SubCategoryId = input.SubCategoryId,
            StartDate = input.StartDate,
            DueDate = input.DueDate,
            ClientCompanyId = companyId,
            OwnerMemberId = ownerId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Projects.Add(item);
        await db.SaveChangesAsync();
        return await GetAsync(item.ProjectId);
    }

    public async Task<ProjectDetailDto> UpdateAsync(long id, ProjectWriteDto input)
    {
        var item = await db.Projects.FirstOrDefaultAsync(x => x.ProjectId == id)
            ?? throw new AppException(404, "找不到該專案");
        await ValidateCategoryAndDates(input.MajorCategoryId, input.SubCategoryId, input.StartDate, input.DueDate);
        item.ProjectCode = await RequireUniqueCode(input.Code, id);
        item.ProjectName = RequireName(input.Name);
        item.Description = TrimText(input.Description, 4000);
        item.MajorCategoryId = input.MajorCategoryId;
        item.SubCategoryId = input.SubCategoryId;
        item.StartDate = input.StartDate;
        item.DueDate = input.DueDate;
        var companyId = await RequireCompany(input.ClientCompanyId);
        if (item.ClientCompanyId != companyId)
        {
            var n1 = await db.TrackTodos.CountAsync(x => x.ProjectId == id && x.TargetType == TrackTodoService.TargetContact);
            var itemIds = db.ProjectIssues.Where(x => x.ProjectId == id).Select(x => x.ProjectIssueId);
            var n2 = await db.TrackTodos.CountAsync(x =>
                x.ProjectIssueId != null && itemIds.Contains(x.ProjectIssueId.Value) && x.TargetType == TrackTodoService.TargetContact);
            var workItemIds = db.ProjectWorkItems.Where(x => x.ProjectId == id).Select(x => x.ProjectWorkItemId);
            var n3 = await db.TrackTodos.CountAsync(x =>
                x.ProjectWorkItemId != null && workItemIds.Contains(x.ProjectWorkItemId.Value) && x.TargetType == TrackTodoService.TargetContact);
            var parts = new List<string>();
            if (n1 > 0) parts.Add($"{n1} 筆專案");
            if (n2 > 0) parts.Add($"{n2} 筆專案議題");
            if (n3 > 0) parts.Add($"{n3} 筆工作項次");
            if (parts.Count > 0)
            {
                throw new AppException(409, "無法更改廠商，尚有 " + string.Join("、", parts) + "的需要追蹤的 TODO 使用客戶窗口");
            }
        }
        item.ClientCompanyId = companyId;
        item.OwnerMemberId = await OptionalMember(input.OwnerMemberId);
        item.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task<(bool Deleted, int ItemCount, int WorkItemCount)> DeleteAsync(long id)
    {
        var item = await db.Projects.FirstOrDefaultAsync(x => x.ProjectId == id)
            ?? throw new AppException(404, "找不到該專案");
        var issues = db.ProjectIssues.Where(x => x.ProjectId == id).ToList();
        var workItems = db.ProjectWorkItems.Where(x => x.ProjectId == id).ToList();
        var count = issues.Count;
        var workCount = workItems.Count;
        var itemIds = issues.Select(x => x.ProjectIssueId).ToList();
        var workIds = workItems.Select(x => x.ProjectWorkItemId).ToList();
        db.TrackTodos.RemoveRange(db.TrackTodos.Where(x =>
            x.ProjectId == id
            || (x.ProjectIssueId != null && itemIds.Contains(x.ProjectIssueId.Value))
            || (x.ProjectWorkItemId != null && workIds.Contains(x.ProjectWorkItemId.Value))));
        db.WorkHours.RemoveRange(db.WorkHours.Where(x =>
            (x.ProjectIssueId != null && itemIds.Contains(x.ProjectIssueId.Value))
            || (x.ProjectWorkItemId != null && workIds.Contains(x.ProjectWorkItemId.Value))));
        db.ProjectWorkItems.RemoveRange(workItems);
        db.ProjectIssues.RemoveRange(issues);
        db.Projects.Remove(item);
        await db.SaveChangesAsync();
        return (true, count, workCount);
    }

    public async Task<List<ProjectIssueDto>> ListItemsAsync(long projectId)
    {
        await EnsureProjectExists(projectId);
        var rows = await db.ProjectIssues
            .Include(x => x.MajorCategory)
            .Include(x => x.SubCategory)
            .Include(x => x.Project).ThenInclude(x => x!.ClientCompany)
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.SeqNo)
            .ThenBy(x => x.ProjectIssueId)
            .ToListAsync();
        var stale = rows.Where(x => x.ActualEndDate != null && !x.IsCompleted).ToList();
        if (stale.Count > 0)
        {
            foreach (var row in stale)
            {
                row.IsCompleted = true;
                row.UpdatedAt = DateTime.Now;
            }
            await db.SaveChangesAsync();
        }
        var ids = rows.Select(x => x.ProjectIssueId).ToList();
        var totals = ids.Count == 0
            ? []
            : await db.WorkHours
                .Where(x => x.ProjectIssueId != null && ids.Contains(x.ProjectIssueId.Value))
                .GroupBy(x => x.ProjectIssueId!.Value)
                .Select(g => new { g.Key, Total = g.Sum(x => x.HourValue) })
                .ToDictionaryAsync(x => x.Key, x => x.Total);
        return rows.Select(x => ToIssueDto(x, totals.GetValueOrDefault(x.ProjectIssueId))).ToList();
    }

    public async Task<List<ProjectIssueDto>> CreateItemAsync(long projectId, ProjectIssueWriteDto input)
    {
        await EnsureProjectExists(projectId);
        await ValidateCategoryAndDates(input.MajorCategoryId, input.SubCategoryId, input.StartDate, input.DueDate);
        var now = DateTime.Now;
        db.ProjectIssues.Add(new ProjectIssue
        {
            ProjectId = projectId,
            SeqNo = await RequireUniqueSeq(projectId, input.SeqNo, null),
            Title = RequireTitle(input.Title),
            Content = TrimText(input.Content, 4000),
            MajorCategoryId = input.MajorCategoryId,
            SubCategoryId = input.SubCategoryId,
            StartDate = input.StartDate,
            DueDate = input.DueDate,
            HandlerName = RequireHandler(input.HandlerName),
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
        return await ListItemsAsync(projectId);
    }

    public async Task<List<ProjectIssueDto>> UpdateItemAsync(long projectId, long id, ProjectIssueWriteDto input)
    {
        var item = await db.ProjectIssues.FirstOrDefaultAsync(x => x.ProjectIssueId == id && x.ProjectId == projectId)
            ?? throw new AppException(404, "找不到該專案議題");
        if (!item.SeqNo.Equals((input.SeqNo ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException(400, "項次建立後不可修改");
        }
        await ValidateCategoryAndDates(input.MajorCategoryId, input.SubCategoryId, input.StartDate, input.DueDate);
        item.Title = RequireTitle(input.Title);
        item.Content = TrimText(input.Content, 4000);
        item.MajorCategoryId = input.MajorCategoryId;
        item.SubCategoryId = input.SubCategoryId;
        item.StartDate = input.StartDate;
        item.DueDate = input.DueDate;
        item.HandlerName = RequireHandler(input.HandlerName);
        item.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await ListItemsAsync(projectId);
    }

    public async Task<ProjectIssueDto> UpdateItemRemarkAsync(long projectId, long id, string? remark)
    {
        var item = await db.ProjectIssues.FirstOrDefaultAsync(x => x.ProjectIssueId == id && x.ProjectId == projectId)
            ?? throw new AppException(404, "找不到該專案議題");
        var text = (remark ?? "").Trim();
        if (text.Length > 2000)
        {
            throw new AppException(400, "備註過長");
        }
        item.Remark = text;
        item.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return (await ListItemsAsync(projectId)).First(x => x.Id == id);
    }

    public async Task<ProjectIssueDto> SetItemCompletedAsync(long projectId, long id, bool isCompleted)
    {
        var item = await db.ProjectIssues.FirstOrDefaultAsync(x => x.ProjectIssueId == id && x.ProjectId == projectId)
            ?? throw new AppException(404, "找不到該專案議題");
        item.IsCompleted = isCompleted;
        if (isCompleted)
        {
            item.ActualEndDate = await db.WorkHours.Where(x => x.ProjectIssueId == id)
                .Select(x => (DateOnly?)x.WorkDate)
                .MaxAsync();
        }
        else
        {
            item.ActualEndDate = null;
        }
        item.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return (await ListItemsAsync(projectId)).First(x => x.Id == id);
    }

    public async Task<List<ProjectIssueDto>> DeleteItemAsync(long projectId, long id)
    {
        var item = await db.ProjectIssues.FirstOrDefaultAsync(x => x.ProjectIssueId == id && x.ProjectId == projectId)
            ?? throw new AppException(404, "找不到該專案議題");
        db.TrackTodos.RemoveRange(db.TrackTodos.Where(x => x.ProjectIssueId == id));
        db.WorkHours.RemoveRange(db.WorkHours.Where(x => x.ProjectIssueId == id));
        db.ProjectIssues.Remove(item);
        await db.SaveChangesAsync();
        return await ListItemsAsync(projectId);
    }

    private async Task<Project> LoadProjectAsync(long id)
    {
        return await db.Projects
            .Include(x => x.MajorCategory)
            .Include(x => x.SubCategory)
            .Include(x => x.ClientCompany)
            .Include(x => x.OwnerMember)
            .FirstOrDefaultAsync(x => x.ProjectId == id)
            ?? throw new AppException(404, "找不到該專案");
    }

    private async Task EnsureProjectExists(long projectId)
    {
        if (!await db.Projects.AnyAsync(x => x.ProjectId == projectId))
        {
            throw new AppException(404, "找不到該專案");
        }
    }

    private async Task ValidateCategoryAndDates(long majorCategoryId, long? subCategoryId, DateOnly? startDate, DateOnly? dueDate)
    {
        if (!await db.MajorCategories.AnyAsync(x => x.MajorCategoryId == majorCategoryId))
        {
            throw new AppException(400, "無效的大分類");
        }
        if (subCategoryId is long subId)
        {
            var sub = await db.SubCategories.FirstOrDefaultAsync(x => x.SubCategoryId == subId)
                ?? throw new AppException(400, "無效的小分類");
            if (sub.MajorCategoryId != majorCategoryId)
            {
                throw new AppException(400, "小分類不屬於所選大分類");
            }
        }
        if (startDate is DateOnly start && dueDate is DateOnly due && start > due)
        {
            throw new AppException(400, "預計開始日不可晚於預計完成日");
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

    private async Task<long?> OptionalMember(long? memberId)
    {
        if (memberId is not long id || id <= 0)
        {
            return null;
        }
        if (!await db.CompanyMembers.AnyAsync(x => x.CompanyMemberId == id))
        {
            throw new AppException(400, "無效的負責人員");
        }
        return id;
    }

    private async Task<string> RequireUniqueCode(string? value, long? excludeId)
    {
        var code = (value ?? "").Trim();
        if (code.Length == 0)
        {
            throw new AppException(400, "專案代號不可空白");
        }
        if (code.Length > 50)
        {
            throw new AppException(400, "專案代號過長");
        }
        var taken = await db.Projects.AnyAsync(x =>
            x.ProjectCode.ToLower() == code.ToLower()
            && (excludeId == null || x.ProjectId != excludeId));
        if (taken)
        {
            throw new AppException(409, "專案代號已存在");
        }
        return code;
    }

    private async Task<string> RequireUniqueSeq(long projectId, string? value, long? excludeId)
    {
        var seq = (value ?? "").Trim();
        if (seq.Length == 0)
        {
            throw new AppException(400, "項次不可空白");
        }
        if (seq.Length > 50)
        {
            throw new AppException(400, "項次過長");
        }
        var taken = await db.ProjectIssues.AnyAsync(x =>
            x.ProjectId == projectId
            && x.SeqNo.ToLower() == seq.ToLower()
            && (excludeId == null || x.ProjectIssueId != excludeId));
        if (taken)
        {
            throw new AppException(409, "同一專案已有相同項次");
        }
        return seq;
    }

    private static string RequireName(string? name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
        {
            throw new AppException(400, "專案名稱不可空白");
        }
        if (trimmed.Length > 200)
        {
            throw new AppException(400, "專案名稱過長");
        }
        return trimmed;
    }

    private static string RequireHandler(string? name)
    {
        var value = name?.Trim() ?? "";
        if (value.Length > 50)
        {
            throw new AppException(400, "處理人員過長");
        }
        return value;
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

    private static string TrimText(string? value, int max)
    {
        var text = value?.Trim() ?? "";
        if (text.Length > max)
        {
            throw new AppException(400, "內容過長");
        }
        return text;
    }

    private static string DisplayColor(string? hex) =>
        ColorPresets.MapOrDefault(hex);

    private static ProjectListItemDto ToListItem(Project x, int itemCount, int workItemCount = 0)
    {
        var color = DisplayColor(x.SubCategory?.ColorHex);
        return new ProjectListItemDto
        {
            Id = x.ProjectId,
            Code = x.ProjectCode,
            Name = x.ProjectName,
            Description = x.Description,
            MajorCategoryId = x.MajorCategoryId,
            MajorCategoryName = x.MajorCategory?.CategoryName ?? "",
            MajorCategoryColor = color,
            SubCategoryId = x.SubCategoryId,
            SubCategoryName = x.SubCategory?.SubCategoryName,
            SubCategoryColor = color,
            StartDate = x.StartDate,
            DueDate = x.DueDate,
            ItemCount = itemCount,
            WorkItemCount = workItemCount,
            ClientCompanyId = x.ClientCompanyId,
            ClientCompanyName = x.ClientCompany?.CompanyName,
            OwnerMemberId = x.OwnerMemberId,
            OwnerMemberName = x.OwnerMember?.MemberName,
            OwnerMemberEnglishName = x.OwnerMember?.EnglishName,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };
    }

    private static ProjectIssueDto ToIssueDto(ProjectIssue x, decimal hoursTotal = 0)
    {
        var color = DisplayColor(x.SubCategory?.ColorHex);
        return new ProjectIssueDto
        {
            Id = x.ProjectIssueId,
            ProjectId = x.ProjectId,
            ProjectCode = x.Project?.ProjectCode ?? "",
            SeqNo = x.SeqNo,
            Title = x.Title,
            Content = x.Content,
            MajorCategoryId = x.MajorCategoryId,
            MajorCategoryName = x.MajorCategory?.CategoryName ?? "",
            MajorCategoryColor = color,
            SubCategoryId = x.SubCategoryId,
            SubCategoryName = x.SubCategory?.SubCategoryName,
            SubCategoryColor = color,
            StartDate = x.StartDate,
            DueDate = x.DueDate,
            ActualStartDate = x.ActualStartDate,
            ActualEndDate = x.ActualEndDate,
            HandlerName = x.HandlerName,
            IsCompleted = x.IsCompleted,
            MissingKept = x.MissingKept,
            Remark = x.Remark,
            HoursTotal = hoursTotal,
            ClientCompanyId = x.Project?.ClientCompanyId,
            ClientCompanyName = x.Project?.ClientCompany?.CompanyName,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };
    }
}
