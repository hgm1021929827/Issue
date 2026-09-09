using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Services;

public class CategoryService(AppDbContext db)
{
    public async Task<List<MajorCategoryDto>> ListMajorAsync()
    {
        return await db.MajorCategories.OrderBy(x => x.SortOrder)
            .Select(x => new MajorCategoryDto
            {
                Id = x.MajorCategoryId,
                Name = x.CategoryName,
                Color = x.ColorHex,
                SortOrder = x.SortOrder
            }).ToListAsync();
    }

    public async Task<List<SubCategoryDto>> ListSubAsync(long? majorCategoryId)
    {
        var query = db.SubCategories.AsQueryable();
        if (majorCategoryId is not null)
        {
            if (!await db.MajorCategories.AnyAsync(x => x.MajorCategoryId == majorCategoryId))
            {
                throw new AppException(400, "無效的大分類");
            }
            query = query.Where(x => x.MajorCategoryId == majorCategoryId);
        }

        return await query.OrderBy(x => x.MajorCategoryId).ThenBy(x => x.SortOrder)
            .Select(x => new SubCategoryDto
            {
                Id = x.SubCategoryId,
                MajorCategoryId = x.MajorCategoryId,
                Name = x.SubCategoryName,
                Color = x.ColorHex,
                SortOrder = x.SortOrder
            }).ToListAsync();
    }

    public async Task<List<MajorCategoryDto>> CreateMajorAsync(MajorCategoryWriteDto input)
    {
        var name = RequireName(input.Name);
        await EnsureUniqueMajorName(name, null);
        var maxSort = await db.MajorCategories.Select(x => (int?)x.SortOrder).MaxAsync() ?? 0;
        db.MajorCategories.Add(new Entities.MajorCategory
        {
            CategoryName = name,
            ColorHex = ColorPresets.Default,
            SortOrder = maxSort + 1
        });
        await db.SaveChangesAsync();
        return await ListMajorAsync();
    }

    public async Task<List<MajorCategoryDto>> UpdateMajorAsync(long id, MajorCategoryWriteDto input)
    {
        var item = await db.MajorCategories.FirstOrDefaultAsync(x => x.MajorCategoryId == id)
            ?? throw new AppException(404, "找不到該大分類");
        var name = RequireName(input.Name);
        await EnsureUniqueMajorName(name, id);
        item.CategoryName = name;
        await db.SaveChangesAsync();
        return await ListMajorAsync();
    }

    public async Task<List<MajorCategoryDto>> DeleteMajorAsync(long id)
    {
        var item = await db.MajorCategories.FirstOrDefaultAsync(x => x.MajorCategoryId == id)
            ?? throw new AppException(404, "找不到該大分類");
        if (await db.MajorCategories.CountAsync() <= 1)
        {
            throw new AppException(409, "至少需保留一個大分類");
        }
        var used = await CountUsageAsync(
            issueCount: () => db.Issues.CountAsync(x => x.MajorCategoryId == id),
            projectCount: () => db.Projects.CountAsync(x => x.MajorCategoryId == id),
            projectIssueCount: () => db.ProjectIssues.CountAsync(x => x.MajorCategoryId == id));
        if (used is not null)
        {
            throw new AppException(409, used);
        }
        db.SubCategories.RemoveRange(db.SubCategories.Where(x => x.MajorCategoryId == id));
        db.MajorCategories.Remove(item);
        await db.SaveChangesAsync();
        return await ListMajorAsync();
    }

    public async Task<List<SubCategoryDto>> CreateSubAsync(SubCategoryWriteDto input)
    {
        var name = RequireName(input.Name);
        if (!await db.MajorCategories.AnyAsync(x => x.MajorCategoryId == input.MajorCategoryId))
        {
            throw new AppException(400, "無效的大分類");
        }
        var color = ResolveColor(input.Color, required: input.Color is not null);
        await EnsureUniqueName(input.MajorCategoryId, name, null);
        var maxSort = await db.SubCategories.Where(x => x.MajorCategoryId == input.MajorCategoryId)
            .Select(x => (int?)x.SortOrder).MaxAsync() ?? 0;
        db.SubCategories.Add(new Entities.SubCategory
        {
            MajorCategoryId = input.MajorCategoryId,
            SubCategoryName = name,
            ColorHex = color,
            SortOrder = maxSort + 1
        });
        await db.SaveChangesAsync();
        return await ListSubAsync(input.MajorCategoryId);
    }

    public async Task<List<SubCategoryDto>> UpdateSubAsync(long id, SubCategoryUpdateDto input)
    {
        var item = await db.SubCategories.FirstOrDefaultAsync(x => x.SubCategoryId == id)
            ?? throw new AppException(404, "找不到該小分類");
        if (input.Name is null && input.Color is null)
        {
            throw new AppException(400, "至少提供名稱或顏色");
        }
        if (input.Name is not null)
        {
            var name = RequireName(input.Name);
            await EnsureUniqueName(item.MajorCategoryId, name, id);
            item.SubCategoryName = name;
        }
        if (input.Color is not null)
        {
            item.ColorHex = ResolveColor(input.Color, required: true);
        }
        await db.SaveChangesAsync();
        return await ListSubAsync(item.MajorCategoryId);
    }

    public async Task<List<SubCategoryDto>> DeleteSubAsync(long id)
    {
        var item = await db.SubCategories.FirstOrDefaultAsync(x => x.SubCategoryId == id)
            ?? throw new AppException(404, "找不到該小分類");
        var used = await CountUsageAsync(
            issueCount: () => db.Issues.CountAsync(x => x.SubCategoryId == id),
            projectCount: () => db.Projects.CountAsync(x => x.SubCategoryId == id),
            projectIssueCount: () => db.ProjectIssues.CountAsync(x => x.SubCategoryId == id));
        if (used is not null)
        {
            throw new AppException(409, used);
        }
        var majorId = item.MajorCategoryId;
        db.SubCategories.Remove(item);
        await db.SaveChangesAsync();
        return await ListSubAsync(majorId);
    }

    private static string ResolveColor(string? color, bool required)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            if (required)
            {
                throw new AppException(400, "顏色必須是 10 個備選色之一");
            }
            return ColorPresets.Default;
        }
        if (!ColorPresets.IsAllowed(color))
        {
            throw new AppException(400, "顏色必須是 10 個備選色之一");
        }
        return ColorPresets.Normalize(color);
    }

    private static string RequireName(string? name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
        {
            throw new AppException(400, "名稱不可空白");
        }
        if (trimmed.Length > 50)
        {
            throw new AppException(400, "名稱過長");
        }
        return trimmed;
    }

    private async Task EnsureUniqueMajorName(string name, long? exceptId)
    {
        var exists = await db.MajorCategories.AnyAsync(x =>
            (exceptId == null || x.MajorCategoryId != exceptId)
            && x.CategoryName.ToLower() == name.ToLower());
        if (exists)
        {
            throw new AppException(409, "已有相同名稱的大分類");
        }
    }

    private async Task EnsureUniqueName(long majorId, string name, long? exceptId)
    {
        var exists = await db.SubCategories.AnyAsync(x =>
            x.MajorCategoryId == majorId
            && x.SubCategoryId != exceptId
            && x.SubCategoryName.ToLower() == name.ToLower());
        if (exists)
        {
            throw new AppException(409, "同一個大分類下已有相同名稱");
        }
    }

    private static async Task<string?> CountUsageAsync(
        Func<Task<int>> issueCount,
        Func<Task<int>> projectCount,
        Func<Task<int>> projectIssueCount)
    {
        var issues = await issueCount();
        var projects = await projectCount();
        var items = await projectIssueCount();
        if (issues + projects + items == 0)
        {
            return null;
        }
        var parts = new List<string>();
        if (issues > 0) parts.Add($"{issues} 筆正式議題");
        if (projects > 0) parts.Add($"{projects} 筆專案");
        if (items > 0) parts.Add($"{items} 筆專案議題");
        return "使用中，共 " + string.Join("、", parts);
    }
}
