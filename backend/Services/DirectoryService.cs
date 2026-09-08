using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Services;

public class DirectoryService(AppDbContext db)
{
    private static readonly HashSet<string> ChannelTypes = ["phone", "mobile", "email", "teams", "other"];

    public async Task<List<CompanyMemberDto>> ListMembersAsync(string? q)
    {
        var query = db.CompanyMembers.AsQueryable();
        var key = (q ?? "").Trim();
        if (key.Length > 0)
        {
            var lower = key.ToLower();
            query = query.Where(x => x.MemberName.ToLower().Contains(lower) || x.EnglishName.ToLower().Contains(lower));
        }
        var rows = await query.OrderBy(x => x.MemberName).ThenBy(x => x.CompanyMemberId).ToListAsync();
        return rows.Select(ToMemberDto).ToList();
    }

    public async Task<CompanyMemberDto> CreateMemberAsync(CompanyMemberWriteDto input)
    {
        var now = DateTime.Now;
        var item = new CompanyMember
        {
            MemberName = RequireText(input.Name, 50, "中文名"),
            EnglishName = RequireText(input.EnglishName, 50, "英文名"),
            Remark = TrimText(input.Remark, 400, "備註"),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.CompanyMembers.Add(item);
        await db.SaveChangesAsync();
        return ToMemberDto(item);
    }

    public async Task<CompanyMemberDto> UpdateMemberAsync(long id, CompanyMemberWriteDto input)
    {
        var item = await db.CompanyMembers.FirstOrDefaultAsync(x => x.CompanyMemberId == id)
            ?? throw new AppException(404, "找不到該公司成員");
        item.MemberName = RequireText(input.Name, 50, "中文名");
        item.EnglishName = RequireText(input.EnglishName, 50, "英文名");
        item.Remark = TrimText(input.Remark, 400, "備註");
        item.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return ToMemberDto(item);
    }

    public async Task DeleteMemberAsync(long id)
    {
        var item = await db.CompanyMembers.FirstOrDefaultAsync(x => x.CompanyMemberId == id)
            ?? throw new AppException(404, "找不到該公司成員");
        var projectCount = await db.Projects.CountAsync(x => x.OwnerMemberId == id);
        var trackCount = await db.TrackTodos.CountAsync(x => x.CompanyMemberId == id);
        var parts = new List<string>();
        if (projectCount > 0) parts.Add($"{projectCount} 筆專案");
        if (trackCount > 0) parts.Add($"{trackCount} 筆需要追蹤的 TODO");
        if (parts.Count > 0)
        {
            throw new AppException(409, "使用中，共 " + string.Join("、", parts));
        }
        db.CompanyMembers.Remove(item);
        await db.SaveChangesAsync();
    }

    public async Task<List<ClientCompanyListItemDto>> ListCompaniesAsync(string? q)
    {
        var query = db.ClientCompanies.AsQueryable();
        var key = (q ?? "").Trim();
        if (key.Length > 0)
        {
            var lower = key.ToLower();
            query = query.Where(x => x.CompanyName.ToLower().Contains(lower));
        }
        var rows = await query.OrderBy(x => x.CompanyName).ThenBy(x => x.ClientCompanyId).ToListAsync();
        return rows.Select(x => new ClientCompanyListItemDto { Id = x.ClientCompanyId, Name = x.CompanyName }).ToList();
    }

    public async Task<List<ClientCompanyTreeDto>> TreeAsync(string? q)
    {
        var rows = await db.ClientCompanies
            .Include(x => x.Contacts).ThenInclude(x => x.Channels)
            .OrderBy(x => x.CompanyName)
            .ThenBy(x => x.ClientCompanyId)
            .ToListAsync();
        var key = (q ?? "").Trim();
        if (key.Length > 0)
        {
            var lower = key.ToLower();
            rows = rows.Where(x =>
                x.CompanyName.ToLower().Contains(lower)
                || x.Contacts.Any(c => c.ContactName.ToLower().Contains(lower))).ToList();
        }
        return rows.Select(ToTreeDto).ToList();
    }

    public async Task<ClientCompanyDto> CreateCompanyAsync(ClientCompanyWriteDto input)
    {
        var now = DateTime.Now;
        var item = new ClientCompany
        {
            CompanyName = await RequireUniqueCompanyName(input.Name, null),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.ClientCompanies.Add(item);
        await db.SaveChangesAsync();
        return ToCompanyDto(item);
    }

    public async Task<ClientCompanyDto> UpdateCompanyAsync(long id, ClientCompanyWriteDto input)
    {
        var item = await db.ClientCompanies.FirstOrDefaultAsync(x => x.ClientCompanyId == id)
            ?? throw new AppException(404, "找不到該客戶公司");
        item.CompanyName = await RequireUniqueCompanyName(input.Name, id);
        item.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return ToCompanyDto(item);
    }

    public async Task DeleteCompanyAsync(long id)
    {
        var item = await db.ClientCompanies.FirstOrDefaultAsync(x => x.ClientCompanyId == id)
            ?? throw new AppException(404, "找不到該客戶公司");
        var projectCount = await db.Projects.CountAsync(x => x.ClientCompanyId == id);
        var issueCount = await db.Issues.CountAsync(x => x.ClientCompanyId == id);
        var trackCount = await db.TrackTodos.CountAsync(x =>
            x.ClientContactId != null
            && db.ClientContacts.Any(c => c.ClientContactId == x.ClientContactId && c.ClientCompanyId == id));
        var parts = new List<string>();
        if (projectCount > 0) parts.Add($"{projectCount} 筆專案");
        if (issueCount > 0) parts.Add($"{issueCount} 筆正式議題");
        if (trackCount > 0) parts.Add($"{trackCount} 筆需要追蹤的 TODO");
        if (parts.Count > 0)
        {
            throw new AppException(409, "使用中，共 " + string.Join("、", parts));
        }
        db.ClientCompanies.Remove(item);
        await db.SaveChangesAsync();
    }

    public async Task<List<ClientContactListItemDto>> ListContactOptionsAsync(long companyId)
    {
        await EnsureCompany(companyId);
        var rows = await db.ClientContacts
            .Where(x => x.ClientCompanyId == companyId)
            .OrderBy(x => x.ContactName)
            .ThenBy(x => x.ClientContactId)
            .ToListAsync();
        return rows.Select(x => new ClientContactListItemDto { Id = x.ClientContactId, Name = x.ContactName }).ToList();
    }

    public async Task<List<ClientContactDto>> CreateContactAsync(long companyId, ClientContactWriteDto input)
    {
        await EnsureCompany(companyId);
        var now = DateTime.Now;
        db.ClientContacts.Add(new ClientContact
        {
            ClientCompanyId = companyId,
            ContactName = await RequireUniqueContactName(companyId, input.Name, null),
            JobTitle = TrimText(input.Title, 50, "職位"),
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
        return await ListContactsAsync(companyId);
    }

    public async Task<List<ClientContactDto>> UpdateContactAsync(long companyId, long id, ClientContactWriteDto input)
    {
        var item = await db.ClientContacts.FirstOrDefaultAsync(x => x.ClientContactId == id && x.ClientCompanyId == companyId)
            ?? throw new AppException(404, "找不到該客戶窗口");
        item.ContactName = await RequireUniqueContactName(companyId, input.Name, id);
        item.JobTitle = TrimText(input.Title, 50, "職位");
        item.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return await ListContactsAsync(companyId);
    }

    public async Task<List<ClientContactDto>> DeleteContactAsync(long companyId, long id)
    {
        var item = await db.ClientContacts.FirstOrDefaultAsync(x => x.ClientContactId == id && x.ClientCompanyId == companyId)
            ?? throw new AppException(404, "找不到該客戶窗口");
        var trackCount = await db.TrackTodos.CountAsync(x => x.ClientContactId == id);
        if (trackCount > 0)
        {
            throw new AppException(409, $"使用中，共 {trackCount} 筆需要追蹤的 TODO");
        }
        db.ClientContacts.Remove(item);
        await db.SaveChangesAsync();
        return await ListContactsAsync(companyId);
    }

    public async Task<List<ContactChannelDto>> CreateChannelAsync(long companyId, long contactId, ContactChannelWriteDto input)
    {
        var contact = await RequireContact(companyId, contactId);
        var channel = new ContactChannel
        {
            ClientContactId = contact.ClientContactId,
            ChannelType = RequireChannelType(input.Type),
            ChannelValue = RequireText(input.Value, 200, "聯絡內容")
        };
        db.ContactChannels.Add(channel);
        await db.SaveChangesAsync();
        return await ListChannelsAsync(contactId);
    }

    public async Task<List<ContactChannelDto>> UpdateChannelAsync(long companyId, long contactId, long channelId, ContactChannelWriteDto input)
    {
        await RequireContact(companyId, contactId);
        var item = await db.ContactChannels.FirstOrDefaultAsync(x => x.ContactChannelId == channelId && x.ClientContactId == contactId)
            ?? throw new AppException(404, "找不到該聯絡資料");
        item.ChannelType = RequireChannelType(input.Type);
        item.ChannelValue = RequireText(input.Value, 200, "聯絡內容");
        await db.SaveChangesAsync();
        return await ListChannelsAsync(contactId);
    }

    public async Task<List<ContactChannelDto>> DeleteChannelAsync(long companyId, long contactId, long channelId)
    {
        await RequireContact(companyId, contactId);
        var item = await db.ContactChannels.FirstOrDefaultAsync(x => x.ContactChannelId == channelId && x.ClientContactId == contactId)
            ?? throw new AppException(404, "找不到該聯絡資料");
        db.ContactChannels.Remove(item);
        await db.SaveChangesAsync();
        return await ListChannelsAsync(contactId);
    }

    private async Task<List<ClientContactDto>> ListContactsAsync(long companyId)
    {
        var rows = await db.ClientContacts.Include(x => x.Channels)
            .Where(x => x.ClientCompanyId == companyId)
            .OrderBy(x => x.ContactName)
            .ThenBy(x => x.ClientContactId)
            .ToListAsync();
        return rows.Select(ToContactDto).ToList();
    }

    private async Task<List<ContactChannelDto>> ListChannelsAsync(long contactId)
    {
        var rows = await db.ContactChannels.Where(x => x.ClientContactId == contactId)
            .OrderBy(x => x.ContactChannelId)
            .ToListAsync();
        return rows.Select(ToChannelDto).ToList();
    }

    private async Task EnsureCompany(long companyId)
    {
        if (!await db.ClientCompanies.AnyAsync(x => x.ClientCompanyId == companyId))
        {
            throw new AppException(404, "找不到該客戶公司");
        }
    }

    private async Task<ClientContact> RequireContact(long companyId, long contactId)
    {
        return await db.ClientContacts.FirstOrDefaultAsync(x => x.ClientContactId == contactId && x.ClientCompanyId == companyId)
            ?? throw new AppException(404, "找不到該客戶窗口");
    }

    private async Task<string> RequireUniqueCompanyName(string? value, long? excludeId)
    {
        var name = RequireText(value, 100, "客戶公司名稱");
        var taken = await db.ClientCompanies.AnyAsync(x =>
            x.CompanyName.ToLower() == name.ToLower()
            && (excludeId == null || x.ClientCompanyId != excludeId));
        if (taken)
        {
            throw new AppException(409, "客戶公司名稱已存在");
        }
        return name;
    }

    private async Task<string> RequireUniqueContactName(long companyId, string? value, long? excludeId)
    {
        var name = RequireText(value, 50, "窗口名稱");
        var taken = await db.ClientContacts.AnyAsync(x =>
            x.ClientCompanyId == companyId
            && x.ContactName.ToLower() == name.ToLower()
            && (excludeId == null || x.ClientContactId != excludeId));
        if (taken)
        {
            throw new AppException(409, "同一公司已有相同窗口名稱");
        }
        return name;
    }

    private static string RequireChannelType(string? type)
    {
        var value = (type ?? "").Trim().ToLower();
        if (!ChannelTypes.Contains(value))
        {
            throw new AppException(400, "無效的聯絡類型");
        }
        return value;
    }

    private static string RequireText(string? value, int max, string label)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0)
        {
            throw new AppException(400, $"{label}不可空白");
        }
        if (text.Length > max)
        {
            throw new AppException(400, $"{label}過長");
        }
        return text;
    }

    private static string TrimText(string? value, int max, string label)
    {
        var text = value?.Trim() ?? "";
        if (text.Length > max)
        {
            throw new AppException(400, $"{label}過長");
        }
        return text;
    }

    private static CompanyMemberDto ToMemberDto(CompanyMember x) => new()
    {
        Id = x.CompanyMemberId,
        Name = x.MemberName,
        EnglishName = x.EnglishName,
        Remark = x.Remark,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };

    private static ClientCompanyDto ToCompanyDto(ClientCompany x) => new()
    {
        Id = x.ClientCompanyId,
        Name = x.CompanyName,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };

    private static ClientCompanyTreeDto ToTreeDto(ClientCompany x) => new()
    {
        Id = x.ClientCompanyId,
        Name = x.CompanyName,
        Contacts = x.Contacts.OrderBy(c => c.ContactName).ThenBy(c => c.ClientContactId).Select(ToContactDto).ToList()
    };

    private static ClientContactDto ToContactDto(ClientContact x) => new()
    {
        Id = x.ClientContactId,
        ClientCompanyId = x.ClientCompanyId,
        Name = x.ContactName,
        Title = x.JobTitle,
        Channels = x.Channels.OrderBy(c => c.ContactChannelId).Select(ToChannelDto).ToList(),
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };

    private static ContactChannelDto ToChannelDto(ContactChannel x) => new()
    {
        Id = x.ContactChannelId,
        Type = x.ChannelType,
        Value = x.ChannelValue
    };
}
