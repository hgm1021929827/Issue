using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Issue.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Tests;

public class TodoServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        db.MajorCategories.Add(new MajorCategory
        {
            MajorCategoryId = 1,
            CategoryName = "處理中",
            ColorHex = "#4A6B82",
            SortOrder = 1
        });
        db.Issues.Add(new IssueItem
        {
            IssueId = 1,
            IssueNo = "1",
            Title = "測試議題",
            Content = "",
            MajorCategoryId = 1,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Completing_last_child_completes_parent_and_cascades()
    {
        await using var db = CreateDb();
        var svc = new TodoService(db);
        await svc.CreateAsync(1, new TodoWriteDto { Title = "父" });
        var roots = await svc.GetTreeAsync(1);
        var parentId = roots[0].Id;
        await svc.CreateAsync(1, new TodoWriteDto { ParentId = parentId, Title = "子" });
        var afterChild = await svc.GetTreeAsync(1);
        var childId = afterChild[0].Children[0].Id;
        await svc.CreateAsync(1, new TodoWriteDto { ParentId = childId, Title = "孫1" });
        await svc.CreateAsync(1, new TodoWriteDto { ParentId = childId, Title = "孫2" });
        var tree = await svc.GetTreeAsync(1);
        var grand1 = tree[0].Children[0].Children[0].Id;
        var grand2 = tree[0].Children[0].Children[1].Id;

        await svc.SetCompletedAsync(grand1, true);
        tree = await svc.GetTreeAsync(1);
        Assert.False(tree[0].IsCompleted);
        Assert.False(tree[0].Children[0].IsCompleted);

        await svc.SetCompletedAsync(grand2, true);
        tree = await svc.GetTreeAsync(1);
        Assert.True(tree[0].Children[0].Children[1].IsCompleted);
        Assert.True(tree[0].Children[0].IsCompleted);
        Assert.True(tree[0].IsCompleted);
    }

    [Fact]
    public async Task Uncompleting_child_does_not_uncomplete_ancestors()
    {
        await using var db = CreateDb();
        var svc = new TodoService(db);
        await svc.CreateAsync(1, new TodoWriteDto { Title = "父" });
        var parentId = (await svc.GetTreeAsync(1))[0].Id;
        await svc.CreateAsync(1, new TodoWriteDto { ParentId = parentId, Title = "子" });
        var childId = (await svc.GetTreeAsync(1))[0].Children[0].Id;
        await svc.SetCompletedAsync(childId, true);
        await svc.SetCompletedAsync(childId, false);
        var tree = await svc.GetTreeAsync(1);
        Assert.False(tree[0].Children[0].IsCompleted);
        Assert.True(tree[0].IsCompleted);
    }

    [Fact]
    public async Task Reorder_rejects_parent_change()
    {
        await using var db = CreateDb();
        var svc = new TodoService(db);
        await svc.CreateAsync(1, new TodoWriteDto { Title = "A" });
        await svc.CreateAsync(1, new TodoWriteDto { Title = "B" });
        var aId = (await svc.GetTreeAsync(1))[0].Id;
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.ReorderAsync(aId, new TodoReorderDto { ParentId = 999, OrderedIds = [aId] }));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Manual_complete_parent_does_not_change_children()
    {
        await using var db = CreateDb();
        var svc = new TodoService(db);
        await svc.CreateAsync(1, new TodoWriteDto { Title = "父" });
        var parentId = (await svc.GetTreeAsync(1))[0].Id;
        await svc.CreateAsync(1, new TodoWriteDto { ParentId = parentId, Title = "子" });
        await svc.SetCompletedAsync(parentId, true);
        var tree = await svc.GetTreeAsync(1);
        Assert.True(tree[0].IsCompleted);
        Assert.False(tree[0].Children[0].IsCompleted);
    }

    [Fact]
    public async Task Create_saves_content()
    {
        await using var db = CreateDb();
        var svc = new TodoService(db);
        var tree = await svc.CreateAsync(1, new TodoWriteDto { Title = "連線", Content = "帶帳號" });
        Assert.Equal("連線", tree[0].Title);
        Assert.Equal("帶帳號", tree[0].Content);
        Assert.Equal(1, tree[0].IssueId);
        Assert.Null(tree[0].ProjectWorkItemId);
    }

    [Fact]
    public async Task Work_item_tree_completes_parent_and_rejects_cross_owner()
    {
        await using var db = CreateDb();
        SeedWorkItem(db);
        var svc = new TodoService(db);
        await svc.CreateForWorkItemAsync(10, 20, new TodoWriteDto { Title = "父", Content = "說明" });
        var parentId = (await svc.GetTreeForWorkItemAsync(10, 20))[0].Id;
        await svc.CreateForWorkItemAsync(10, 20, new TodoWriteDto { ParentId = parentId, Title = "子1" });
        await svc.CreateForWorkItemAsync(10, 20, new TodoWriteDto { ParentId = parentId, Title = "子2" });
        var tree = await svc.GetTreeForWorkItemAsync(10, 20);
        Assert.Equal("說明", tree[0].Content);
        Assert.Equal(20, tree[0].ProjectWorkItemId);
        Assert.Null(tree[0].IssueId);

        var child1 = tree[0].Children[0].Id;
        var child2 = tree[0].Children[1].Id;
        await svc.SetCompletedAsync(child1, true);
        await svc.SetCompletedAsync(child2, true);
        tree = await svc.GetTreeForWorkItemAsync(10, 20);
        Assert.True(tree[0].IsCompleted);

        var issueTree = await svc.CreateAsync(1, new TodoWriteDto { Title = "議題待辦" });
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.CreateForWorkItemAsync(10, 20, new TodoWriteDto { ParentId = issueTree[0].Id, Title = "誤掛" }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Deleting_work_item_removes_todos()
    {
        await using var db = CreateDb();
        SeedWorkItem(db);
        var todos = new TodoService(db);
        await todos.CreateForWorkItemAsync(10, 20, new TodoWriteDto { Title = "步驟" });
        var workItems = new WorkItemService(db);
        await workItems.DeleteAsync(10, 20);
        Assert.Empty(db.IssueTodos.Where(x => x.ProjectWorkItemId == 20));
    }

    [Fact]
    public async Task Deleting_project_removes_work_item_todos()
    {
        await using var db = CreateDb();
        SeedWorkItem(db);
        var todos = new TodoService(db);
        await todos.CreateForWorkItemAsync(10, 20, new TodoWriteDto { Title = "步驟" });
        var projects = new ProjectService(db);
        await projects.DeleteAsync(10);
        Assert.Empty(db.IssueTodos.Where(x => x.ProjectWorkItemId == 20));
    }

    private static void SeedWorkItem(AppDbContext db)
    {
        db.Projects.Add(new Project
        {
            ProjectId = 10,
            ProjectCode = "P-1",
            ProjectName = "測試專案",
            Description = "",
            MajorCategoryId = 1,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.ProjectWorkItems.Add(new ProjectWorkItem
        {
            ProjectWorkItemId = 20,
            ProjectId = 10,
            WorkItemCode = "W-1",
            Title = "項次",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
    }
}
