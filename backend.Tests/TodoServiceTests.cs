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
}
