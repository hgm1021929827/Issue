using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Services;

public class TodoService(AppDbContext db)
{
    public async Task<List<TodoNodeDto>> GetTreeAsync(long issueId)
    {
        await EnsureIssue(issueId);
        var rows = await db.IssueTodos.Where(x => x.IssueId == issueId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.TodoId).ToListAsync();
        return BuildTree(rows, null);
    }

    public async Task<List<TodoNodeDto>> CreateAsync(long issueId, TodoWriteDto input)
    {
        await EnsureIssue(issueId);
        var title = RequireTitle(input.Title);
        if (input.ParentId is long parentId)
        {
            var parent = await db.IssueTodos.FirstOrDefaultAsync(x => x.TodoId == parentId)
                ?? throw new AppException(404, "找不到父 TODO");
            if (parent.IssueId != issueId)
            {
                throw new AppException(400, "父 TODO 不屬於此議題");
            }
        }

        var maxSort = await db.IssueTodos
            .Where(x => x.IssueId == issueId && x.ParentTodoId == input.ParentId)
            .Select(x => (int?)x.SortOrder).MaxAsync() ?? 0;
        var now = DateTime.Now;
        db.IssueTodos.Add(new IssueTodo
        {
            IssueId = issueId,
            ParentTodoId = input.ParentId,
            Title = title,
            Content = input.Content?.Trim() ?? "",
            SortOrder = maxSort + 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
        return await GetTreeAsync(issueId);
    }

    public async Task<List<TodoNodeDto>> UpdateAsync(long id, TodoUpdateDto input)
    {
        var todo = await GetTodo(id);
        todo.Title = RequireTitle(input.Title);
        todo.Content = input.Content?.Trim() ?? "";
        todo.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await GetTreeAsync(todo.IssueId);
    }

    public async Task<List<TodoNodeDto>> SetCompletedAsync(long id, bool isCompleted)
    {
        var todo = await GetTodo(id);
        if (todo.IsCompleted == isCompleted)
        {
            return await GetTreeAsync(todo.IssueId);
        }

        todo.IsCompleted = isCompleted;
        todo.UpdatedAt = DateTime.Now;
        if (isCompleted)
        {
            await CascadeCompleteParentsAsync(todo);
        }
        await db.SaveChangesAsync();
        return await GetTreeAsync(todo.IssueId);
    }

    public async Task<List<TodoNodeDto>> ReorderAsync(long id, TodoReorderDto input)
    {
        var todo = await GetTodo(id);
        if (todo.ParentTodoId != input.ParentId)
        {
            throw new AppException(409, "本輪僅支援同層排序，不可改掛父項");
        }

        var siblings = await db.IssueTodos
            .Where(x => x.IssueId == todo.IssueId && x.ParentTodoId == input.ParentId)
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
        return await GetTreeAsync(todo.IssueId);
    }

    public async Task<List<TodoNodeDto>> DeleteAsync(long id)
    {
        var todo = await GetTodo(id);
        var issueId = todo.IssueId;
        var all = await db.IssueTodos.Where(x => x.IssueId == issueId).ToListAsync();
        var removeIds = new HashSet<long>();
        CollectDescendants(all, id, removeIds);
        removeIds.Add(id);
        db.IssueTodos.RemoveRange(all.Where(x => removeIds.Contains(x.TodoId)));
        await db.SaveChangesAsync();
        return await GetTreeAsync(issueId);
    }

    private async Task CascadeCompleteParentsAsync(IssueTodo todo)
    {
        if (todo.ParentTodoId is null)
        {
            return;
        }

        var siblings = await db.IssueTodos
            .Where(x => x.IssueId == todo.IssueId && x.ParentTodoId == todo.ParentTodoId)
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

    private async Task<IssueTodo> GetTodo(long id)
    {
        return await db.IssueTodos.FirstOrDefaultAsync(x => x.TodoId == id)
            ?? throw new AppException(404, "找不到該 TODO");
    }

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
                ParentId = x.ParentTodoId,
                IsCompleted = x.IsCompleted,
                Title = x.Title,
                Content = x.Content,
                SortOrder = x.SortOrder,
                Children = BuildTree(rows, x.TodoId)
            }).ToList();
    }
}
