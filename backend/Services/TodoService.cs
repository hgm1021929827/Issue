using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Services;

public class TodoService(AppDbContext db)
{
    private readonly record struct TodoOwner(long? IssueId, long? ProjectId, long? WorkItemId);

    public Task<List<TodoNodeDto>> GetTreeAsync(long issueId) =>
        GetTreeByOwnerAsync(new TodoOwner(issueId, null, null));

    public async Task<List<TodoNodeDto>> GetTreeForProjectAsync(long projectId)
    {
        await EnsureProject(projectId);
        return await GetTreeByOwnerAsync(new TodoOwner(null, projectId, null));
    }

    public async Task<List<TodoNodeDto>> GetTreeForWorkItemAsync(long projectId, long workItemId)
    {
        await EnsureWorkItem(projectId, workItemId);
        return await GetTreeByOwnerAsync(new TodoOwner(null, null, workItemId));
    }

    public Task<List<TodoNodeDto>> CreateAsync(long issueId, TodoWriteDto input) =>
        CreateForOwnerAsync(new TodoOwner(issueId, null, null), input);

    public async Task<List<TodoNodeDto>> CreateForProjectAsync(long projectId, TodoWriteDto input)
    {
        await EnsureProject(projectId);
        return await CreateForOwnerAsync(new TodoOwner(null, projectId, null), input);
    }

    public async Task<List<TodoNodeDto>> CreateForWorkItemAsync(long projectId, long workItemId, TodoWriteDto input)
    {
        await EnsureWorkItem(projectId, workItemId);
        return await CreateForOwnerAsync(new TodoOwner(null, null, workItemId), input);
    }

    public async Task<List<TodoNodeDto>> UpdateAsync(long id, TodoUpdateDto input)
    {
        var todo = await GetTodo(id);
        todo.Title = RequireTitle(input.Title);
        todo.Content = input.Content?.Trim() ?? "";
        todo.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await GetTreeByOwnerAsync(OwnerOf(todo));
    }

    public async Task<List<TodoNodeDto>> SetCompletedAsync(long id, bool isCompleted)
    {
        var todo = await GetTodo(id);
        if (todo.IsCompleted == isCompleted)
        {
            return await GetTreeByOwnerAsync(OwnerOf(todo));
        }

        todo.IsCompleted = isCompleted;
        todo.UpdatedAt = DateTime.Now;
        if (isCompleted)
        {
            await CascadeCompleteParentsAsync(todo);
        }
        await db.SaveChangesAsync();
        return await GetTreeByOwnerAsync(OwnerOf(todo));
    }

    public async Task<List<TodoNodeDto>> ReorderAsync(long id, TodoReorderDto input)
    {
        var todo = await GetTodo(id);
        if (todo.ParentTodoId != input.ParentId)
        {
            throw new AppException(409, "本輪僅支援同層排序，不可改掛父項");
        }

        var siblings = await OwnerQuery(OwnerOf(todo))
            .Where(x => x.ParentTodoId == input.ParentId)
            .ToListAsync();
        var existingIds = siblings.Select(x => x.TodoId).OrderBy(x => x).ToList();
        var sentIds = input.OrderedIds.OrderBy(x => x).ToList();
        if (existingIds.Count != sentIds.Count || !existingIds.SequenceEqual(sentIds))
        {
            throw new AppException(409, "排序清單必須是該父項下的全部子項");
        }

        for (var i = 0; i < input.OrderedIds.Count; i++)
        {
            var row = siblings.First(x => x.TodoId == input.OrderedIds[i]);
            row.SortOrder = i + 1;
            row.UpdatedAt = DateTime.Now;
        }
        await db.SaveChangesAsync();
        return await GetTreeByOwnerAsync(OwnerOf(todo));
    }

    public async Task<List<TodoNodeDto>> DeleteAsync(long id)
    {
        var todo = await GetTodo(id);
        var owner = OwnerOf(todo);
        var all = await OwnerQuery(owner).ToListAsync();
        var removeIds = new HashSet<long>();
        CollectDescendants(all, id, removeIds);
        removeIds.Add(id);
        db.ConnectionAppointmentItems.RemoveRange(
            db.ConnectionAppointmentItems.Where(x => x.TodoId != null && removeIds.Contains(x.TodoId.Value)));
        db.IssueTodos.RemoveRange(all.Where(x => removeIds.Contains(x.TodoId)));
        await db.SaveChangesAsync();
        return await GetTreeByOwnerAsync(owner);
    }

    private async Task<List<TodoNodeDto>> CreateForOwnerAsync(TodoOwner owner, TodoWriteDto input)
    {
        if (owner.IssueId is long iid)
        {
            await EnsureIssue(iid);
        }

        var title = RequireTitle(input.Title);
        if (input.ParentId is long parentId)
        {
            var parent = await db.IssueTodos.FirstOrDefaultAsync(x => x.TodoId == parentId)
                ?? throw new AppException(404, "找不到父 TODO");
            if (parent.IssueId != owner.IssueId || parent.ProjectId != owner.ProjectId || parent.ProjectWorkItemId != owner.WorkItemId)
            {
                throw new AppException(400, "父 TODO 不屬於此工作");
            }
        }

        var maxSort = await OwnerQuery(owner)
            .Where(x => x.ParentTodoId == input.ParentId)
            .Select(x => (int?)x.SortOrder).MaxAsync() ?? 0;
        var now = DateTime.Now;
        db.IssueTodos.Add(new IssueTodo
        {
            IssueId = owner.IssueId,
            ProjectId = owner.ProjectId,
            ProjectWorkItemId = owner.WorkItemId,
            ParentTodoId = input.ParentId,
            Title = title,
            Content = input.Content?.Trim() ?? "",
            SortOrder = maxSort + 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
        return await GetTreeByOwnerAsync(owner);
    }

    private async Task<List<TodoNodeDto>> GetTreeByOwnerAsync(TodoOwner owner)
    {
        if (owner.IssueId is long iid)
        {
            await EnsureIssue(iid);
        }

        var rows = await OwnerQuery(owner)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.TodoId).ToListAsync();
        return BuildTree(rows, null);
    }

    private IQueryable<IssueTodo> OwnerQuery(TodoOwner owner)
    {
        if (owner.IssueId is long iid)
        {
            return db.IssueTodos.Where(x => x.IssueId == iid);
        }

        if (owner.ProjectId is long pid)
        {
            return db.IssueTodos.Where(x => x.ProjectId == pid);
        }

        return db.IssueTodos.Where(x => x.ProjectWorkItemId == owner.WorkItemId);
    }

    private async Task CascadeCompleteParentsAsync(IssueTodo todo)
    {
        if (todo.ParentTodoId is null)
        {
            return;
        }

        var siblings = await OwnerQuery(OwnerOf(todo))
            .Where(x => x.ParentTodoId == todo.ParentTodoId)
            .ToListAsync();
        if (!siblings.All(s => s.IsCompleted))
        {
            return;
        }

        var parent = await db.IssueTodos.FirstAsync(x => x.TodoId == todo.ParentTodoId);
        if (parent.IsCompleted)
        {
            return;
        }

        parent.IsCompleted = true;
        parent.UpdatedAt = DateTime.Now;
        await CascadeCompleteParentsAsync(parent);
    }

    private async Task EnsureIssue(long issueId)
    {
        if (!await db.Issues.AnyAsync(x => x.IssueId == issueId))
        {
            throw new AppException(404, "找不到該議題");
        }
    }

    private async Task EnsureProject(long projectId)
    {
        if (!await db.Projects.AnyAsync(x => x.ProjectId == projectId))
        {
            throw new AppException(404, "找不到該專案");
        }
    }

    private async Task EnsureWorkItem(long projectId, long workItemId)
    {
        if (!await db.ProjectWorkItems.AnyAsync(x => x.ProjectWorkItemId == workItemId && x.ProjectId == projectId))
        {
            throw new AppException(404, "找不到該工作項次");
        }
    }

    private async Task<IssueTodo> GetTodo(long id)
    {
        return await db.IssueTodos.FirstOrDefaultAsync(x => x.TodoId == id)
            ?? throw new AppException(404, "找不到該 TODO");
    }

    private static TodoOwner OwnerOf(IssueTodo todo) =>
        new(todo.IssueId, todo.ProjectId, todo.ProjectWorkItemId);

    private static string RequireTitle(string? title)
    {
        var trimmed = (title ?? "").Trim();
        if (trimmed.Length == 0)
        {
            throw new AppException(400, "標題不可空白");
        }
        return trimmed;
    }

    private static void CollectDescendants(List<IssueTodo> all, long parentId, HashSet<long> acc)
    {
        foreach (var child in all.Where(x => x.ParentTodoId == parentId))
        {
            if (acc.Add(child.TodoId))
            {
                CollectDescendants(all, child.TodoId, acc);
            }
        }
    }

    private static List<TodoNodeDto> BuildTree(List<IssueTodo> rows, long? parentId)
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
                Children = BuildTree(rows, x.TodoId)
            }).ToList();
    }
}
