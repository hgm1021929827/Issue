using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Services;

public class AppointmentService(AppDbContext db)
{
    public const string EndedComplete = "連線完成";
    public const string EndedCancel = "連線取消";
    public const string AppointmentMajorName = "預約";
    public const string DefaultStatusName = "已發信";

    public async Task<AppointmentStatusDto> GetStatusOptionsAsync()
    {
        var major = await FindAppointmentMajorAsync();
        if (major is null)
        {
            return new AppointmentStatusDto();
        }

        var subs = await db.SubCategories
            .Where(x => x.MajorCategoryId == major.MajorCategoryId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.SubCategoryId)
            .ToListAsync();
        return new AppointmentStatusDto
        {
            MajorCategoryId = major.MajorCategoryId,
            Name = major.CategoryName,
            Color = major.ColorHex,
            SubCategories = subs.Select(x => new SubCategoryDto
            {
                Id = x.SubCategoryId,
                MajorCategoryId = x.MajorCategoryId,
                Name = x.SubCategoryName,
                Color = x.ColorHex,
                SortOrder = x.SortOrder
            }).ToList()
        };
    }

    public async Task<List<AppointmentDto>> ListAsync()
    {
        var rows = await QueryAppointments()
            .OrderBy(x => x.AppointmentDate == null)
            .ThenBy(x => x.AppointmentDate)
            .ThenBy(x => x.ConnectionAppointmentId)
            .ToListAsync();
        return await ToDtos(rows);
    }

    public async Task<List<AppointmentDto>> ListFutureAsync(long clientCompanyId, long? excludeId)
    {
        await EnsureCompany(clientCompanyId);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var rows = await QueryAppointments()
            .Where(x => x.ClientCompanyId == clientCompanyId
                && x.AppointmentDate != null
                && x.AppointmentDate > today
                && (excludeId == null || x.ConnectionAppointmentId != excludeId))
            .ToListAsync();
        var open = rows
            .Where(x => !IsEnded(x.SubCategory?.SubCategoryName))
            .OrderBy(x => x.AppointmentDate)
            .ThenBy(x => x.ConnectionAppointmentId)
            .ToList();
        return await ToDtos(open);
    }

    public async Task<AppointmentDto> GetAsync(long id) =>
        (await ToDtos([await RequireAppointment(id)]))[0];

    public async Task<AppointmentDto> CreateAsync(AppointmentWriteDto input)
    {
        await ValidateHeader(input.ClientCompanyId, input.ClientContactId, input.ContactChannelId, input.SubCategoryId);
        var now = DateTime.Now;
        var row = new ConnectionAppointment
        {
            ClientCompanyId = input.ClientCompanyId,
            ClientContactId = input.ClientContactId,
            ContactChannelId = input.ContactChannelId,
            SubCategoryId = input.SubCategoryId,
            AppointmentDate = input.AppointmentDate,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.ConnectionAppointments.Add(row);
        await db.SaveChangesAsync();
        if (input.Items.Count > 0)
        {
            await AddItemsCore(row.ConnectionAppointmentId, input.Items);
        }
        return await GetAsync(row.ConnectionAppointmentId);
    }

    public async Task<AppointmentDto> UpdateAsync(long id, AppointmentWriteDto input)
    {
        var row = await db.ConnectionAppointments.FirstOrDefaultAsync(x => x.ConnectionAppointmentId == id)
            ?? throw new AppException(404, "找不到該預約連線");
        await ValidateHeader(input.ClientCompanyId, input.ClientContactId, input.ContactChannelId, input.SubCategoryId);
        row.ClientCompanyId = input.ClientCompanyId;
        row.ClientContactId = input.ClientContactId;
        row.ContactChannelId = input.ContactChannelId;
        row.SubCategoryId = input.SubCategoryId;
        row.AppointmentDate = input.AppointmentDate;
        row.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task DeleteAsync(long id)
    {
        var row = await db.ConnectionAppointments.FirstOrDefaultAsync(x => x.ConnectionAppointmentId == id)
            ?? throw new AppException(404, "找不到該預約連線");
        db.ConnectionAppointmentItems.RemoveRange(
            db.ConnectionAppointmentItems.Where(x => x.ConnectionAppointmentId == id));
        db.ConnectionAppointments.Remove(row);
        await db.SaveChangesAsync();
    }

    public async Task<AppointmentDto> AddItemsAsync(long id, AppointmentItemsWriteDto input)
    {
        if (!await db.ConnectionAppointments.AnyAsync(x => x.ConnectionAppointmentId == id))
        {
            throw new AppException(404, "找不到該預約連線");
        }
        if (input.Items.Count == 0)
        {
            throw new AppException(400, "請提供處理事項");
        }
        await AddItemsCore(id, input.Items);
        return await GetAsync(id);
    }

    public async Task<AppointmentDto> DeleteItemAsync(long id, long itemId)
    {
        var item = await db.ConnectionAppointmentItems
            .FirstOrDefaultAsync(x => x.ItemId == itemId && x.ConnectionAppointmentId == id)
            ?? throw new AppException(404, "找不到該處理事項");
        db.ConnectionAppointmentItems.Remove(item);
        var parent = await db.ConnectionAppointments.FirstAsync(x => x.ConnectionAppointmentId == id);
        parent.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    private async Task AddItemsCore(long appointmentId, List<AppointmentItemWriteDto> items)
    {
        var maxSort = await db.ConnectionAppointmentItems
            .Where(x => x.ConnectionAppointmentId == appointmentId)
            .Select(x => (int?)x.SortOrder).MaxAsync() ?? 0;
        foreach (var input in items)
        {
            var (issueId, projectId, todoId) = await ResolveTarget(input);
            var duplicate = await db.ConnectionAppointmentItems.AnyAsync(x =>
                x.ConnectionAppointmentId == appointmentId
                && ((issueId != null && x.IssueId == issueId)
                    || (projectId != null && x.ProjectId == projectId)
                    || (todoId != null && x.TodoId == todoId)));
            if (duplicate)
            {
                throw new AppException(409, "此處理事項已在本預約中");
            }
            maxSort++;
            db.ConnectionAppointmentItems.Add(new ConnectionAppointmentItem
            {
                ConnectionAppointmentId = appointmentId,
                IssueId = issueId,
                ProjectId = projectId,
                TodoId = todoId,
                SortOrder = maxSort
            });
        }
        var parent = await db.ConnectionAppointments.FirstAsync(x => x.ConnectionAppointmentId == appointmentId);
        parent.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    private async Task<(long? IssueId, long? ProjectId, long? TodoId)> ResolveTarget(AppointmentItemWriteDto input)
    {
        var filled = (input.IssueId is null ? 0 : 1) + (input.ProjectId is null ? 0 : 1) + (input.TodoId is null ? 0 : 1);
        if (filled != 1)
        {
            throw new AppException(400, "處理事項必須恰好指定議題、專案或待辦之一");
        }

        if (input.IssueId is long issueId)
        {
            if (!await db.Issues.AnyAsync(x => x.IssueId == issueId))
            {
                throw new AppException(404, "找不到該議題");
            }
            return (issueId, null, null);
        }

        if (input.ProjectId is long projectId)
        {
            if (!await db.Projects.AnyAsync(x => x.ProjectId == projectId))
            {
                throw new AppException(404, "找不到該專案");
            }
            return (null, projectId, null);
        }

        var todo = await db.IssueTodos.FirstOrDefaultAsync(x => x.TodoId == input.TodoId)
            ?? throw new AppException(404, "找不到該 TODO");
        if (todo.ProjectWorkItemId is not null || (todo.IssueId is null && todo.ProjectId is null))
        {
            throw new AppException(400, "連線處理事項不可掛工作項次待辦");
        }
        return (null, null, todo.TodoId);
    }

    private async Task ValidateHeader(long companyId, long contactId, long? contactChannelId, long? subCategoryId)
    {
        await EnsureCompany(companyId);
        var contact = await db.ClientContacts.FirstOrDefaultAsync(x => x.ClientContactId == contactId)
            ?? throw new AppException(404, "找不到該客戶窗口");
        if (contact.ClientCompanyId != companyId)
        {
            throw new AppException(400, "窗口不屬於該客戶公司");
        }
        if (contactChannelId is long channelId)
        {
            var channel = await db.ContactChannels.FirstOrDefaultAsync(x => x.ContactChannelId == channelId)
                ?? throw new AppException(400, "找不到該聯繫方式");
            if (channel.ClientContactId != contactId)
            {
                throw new AppException(400, "聯繫方式不屬於該客戶窗口");
            }
        }
        if (subCategoryId is long statusId)
        {
            var major = await FindAppointmentMajorAsync()
                ?? throw new AppException(400, "找不到大分類「預約」");
            var sub = await db.SubCategories.FirstOrDefaultAsync(x => x.SubCategoryId == statusId)
                ?? throw new AppException(400, "無效的預約狀態");
            if (sub.MajorCategoryId != major.MajorCategoryId)
            {
                throw new AppException(400, "預約狀態必須是大分類「預約」底下的小分類");
            }
        }
    }

    private async Task EnsureCompany(long companyId)
    {
        if (!await db.ClientCompanies.AnyAsync(x => x.ClientCompanyId == companyId))
        {
            throw new AppException(404, "找不到該客戶公司");
        }
    }

    private IQueryable<ConnectionAppointment> QueryAppointments() =>
        db.ConnectionAppointments
            .Include(x => x.ClientCompany)
            .Include(x => x.ClientContact)
            .Include(x => x.ContactChannel)
            .Include(x => x.SubCategory).ThenInclude(x => x!.MajorCategory);

    private async Task<ConnectionAppointment> RequireAppointment(long id) =>
        await QueryAppointments().FirstOrDefaultAsync(x => x.ConnectionAppointmentId == id)
            ?? throw new AppException(404, "找不到該預約連線");

    private async Task<MajorCategory?> FindAppointmentMajorAsync()
    {
        var majors = await db.MajorCategories.ToListAsync();
        return majors.FirstOrDefault(x =>
            x.CategoryName.Trim().Equals(AppointmentMajorName, StringComparison.OrdinalIgnoreCase));
    }

    public static string FormatChannel(ContactChannel? channel)
    {
        if (channel is null)
        {
            return "";
        }
        var type = channel.ChannelType switch
        {
            "phone" => "市話",
            "mobile" => "手機",
            "email" => "Email",
            "teams" => "Teams",
            "other" => "其他",
            _ => channel.ChannelType
        };
        return string.IsNullOrWhiteSpace(channel.ChannelValue) ? type : $"{type} {channel.ChannelValue}";
    }

    public static bool IsEnded(string? statusName)
    {
        var name = (statusName ?? "").Trim();
        return name.Equals(EndedComplete, StringComparison.OrdinalIgnoreCase)
            || name.Equals(EndedCancel, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<AppointmentDto>> ToDtos(List<ConnectionAppointment> rows)
    {
        if (rows.Count == 0)
        {
            return [];
        }
        var ids = rows.Select(x => x.ConnectionAppointmentId).ToList();
        var itemRows = await db.ConnectionAppointmentItems
            .Include(x => x.Issue)
            .Include(x => x.Project)
            .Include(x => x.Todo)
            .Where(x => ids.Contains(x.ConnectionAppointmentId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ItemId)
            .ToListAsync();
        var todoIds = itemRows.Where(x => x.TodoId != null).Select(x => x.TodoId!.Value).ToList();
        var todos = await db.IssueTodos.Where(x => todoIds.Contains(x.TodoId)).ToListAsync();
        var issueTodoOwners = todos.Where(x => x.IssueId != null).Select(x => x.IssueId!.Value).Distinct().ToList();
        var projectTodoOwners = todos.Where(x => x.ProjectId != null).Select(x => x.ProjectId!.Value).Distinct().ToList();
        var relatedTodos = await db.IssueTodos
            .Where(x => (x.IssueId != null && issueTodoOwners.Contains(x.IssueId.Value))
                || (x.ProjectId != null && projectTodoOwners.Contains(x.ProjectId.Value)))
            .ToListAsync();
        var issueIds = relatedTodos.Where(x => x.IssueId != null).Select(x => x.IssueId!.Value)
            .Concat(itemRows.Where(x => x.IssueId != null).Select(x => x.IssueId!.Value))
            .Distinct().ToList();
        var projectIds = relatedTodos.Where(x => x.ProjectId != null).Select(x => x.ProjectId!.Value)
            .Concat(itemRows.Where(x => x.ProjectId != null).Select(x => x.ProjectId!.Value))
            .Distinct().ToList();
        var issues = await db.Issues.Where(x => issueIds.Contains(x.IssueId)).ToDictionaryAsync(x => x.IssueId);
        var projects = await db.Projects.Where(x => projectIds.Contains(x.ProjectId)).ToDictionaryAsync(x => x.ProjectId);
        var major = await FindAppointmentMajorAsync();
        var statusColor = major?.ColorHex ?? "";

        return rows.Select(row =>
        {
            var items = itemRows.Where(x => x.ConnectionAppointmentId == row.ConnectionAppointmentId)
                .Select(item => ToItemDto(item, relatedTodos, issues, projects))
                .ToList();
            var titles = items.Select(x => x.Title).Where(x => x.Length > 0).ToList();
            var summary = titles.Count == 0
                ? "（無處理事項）"
                : titles.Count <= 3
                    ? string.Join("、", titles)
                    : string.Join("、", titles.Take(3)) + $" 等 {titles.Count} 筆";
            return new AppointmentDto
            {
                Id = row.ConnectionAppointmentId,
                ClientCompanyId = row.ClientCompanyId,
                ClientCompanyName = row.ClientCompany?.CompanyName ?? "",
                ClientContactId = row.ClientContactId,
                ClientContactName = row.ClientContact?.ContactName ?? "",
                ContactChannelId = row.ContactChannelId,
                ContactChannelLabel = FormatChannel(row.ContactChannel),
                SubCategoryId = row.SubCategoryId,
                StatusName = row.SubCategory?.SubCategoryName ?? "",
                StatusColor = statusColor,
                AppointmentDate = row.AppointmentDate,
                ItemSummary = summary,
                Items = items,
                CreatedAt = row.CreatedAt,
                UpdatedAt = row.UpdatedAt
            };
        }).ToList();
    }

    private static AppointmentItemDto ToItemDto(
        ConnectionAppointmentItem item,
        List<IssueTodo> relatedTodos,
        Dictionary<long, IssueItem> issues,
        Dictionary<long, Project> projects)
    {
        if (item.IssueId is long issueId)
        {
            issues.TryGetValue(issueId, out var issue);
            var title = issue is null ? "議題" : string.IsNullOrWhiteSpace(issue.IssueNo)
                ? issue.Title
                : $"#{issue.IssueNo} {issue.Title}".Trim();
            return new AppointmentItemDto
            {
                Id = item.ItemId,
                Kind = "issue",
                IssueId = issueId,
                Title = title,
                WorkLabel = title,
                Content = issue?.Content ?? ""
            };
        }

        if (item.ProjectId is long projectId)
        {
            projects.TryGetValue(projectId, out var project);
            var title = project is null
                ? "專案"
                : string.IsNullOrWhiteSpace(project.ProjectName)
                    ? project.ProjectCode
                    : $"{project.ProjectCode} {project.ProjectName}".Trim();
            return new AppointmentItemDto
            {
                Id = item.ItemId,
                Kind = "project",
                ProjectId = projectId,
                Title = title,
                WorkLabel = title,
                Content = project?.Description ?? ""
            };
        }

        var todo = item.Todo ?? relatedTodos.FirstOrDefault(x => x.TodoId == item.TodoId);
        var ownerRows = todo?.IssueId is long todoIssueId
            ? relatedTodos.Where(x => x.IssueId == todoIssueId).ToList()
            : todo?.ProjectId is long todoProjectId
                ? relatedTodos.Where(x => x.ProjectId == todoProjectId).ToList()
                : [];
        string workLabel;
        if (todo?.IssueId is long iid && issues.TryGetValue(iid, out var ownerIssue))
        {
            workLabel = string.IsNullOrWhiteSpace(ownerIssue.IssueNo)
                ? ownerIssue.Title
                : $"#{ownerIssue.IssueNo} {ownerIssue.Title}".Trim();
        }
        else if (todo?.ProjectId is long pid && projects.TryGetValue(pid, out var ownerProject))
        {
            workLabel = string.IsNullOrWhiteSpace(ownerProject.ProjectName)
                ? ownerProject.ProjectCode
                : $"{ownerProject.ProjectCode} {ownerProject.ProjectName}".Trim();
        }
        else
        {
            workLabel = "待辦";
        }

        return new AppointmentItemDto
        {
            Id = item.ItemId,
            Kind = "todo",
            IssueId = todo?.IssueId,
            ProjectId = todo?.ProjectId,
            TodoId = item.TodoId,
            WorkLabel = workLabel,
            Title = todo?.Title ?? "",
            Content = todo?.Content ?? "",
            Children = BuildTodoChildren(ownerRows, item.TodoId)
        };
    }

    private static List<TodoNodeDto> BuildTodoChildren(List<IssueTodo> rows, long? parentId)
    {
        return rows.Where(x => x.ParentTodoId == parentId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.TodoId)
            .Select(x => new TodoNodeDto
            {
                Id = x.TodoId,
                IssueId = x.IssueId,
                ProjectId = x.ProjectId,
                ProjectWorkItemId = x.ProjectWorkItemId,
                ParentId = x.ParentTodoId,
                IsCompleted = x.IsCompleted,
                Title = x.Title,
                Content = x.Content,
                SortOrder = x.SortOrder,
                Children = BuildTodoChildren(rows, x.TodoId)
            }).ToList();
    }
}
