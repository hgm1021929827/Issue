using System.Globalization;
using System.Text;
using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Issue.Api.Services;

public class IssueImportService(AppDbContext db, ImportNotFoundCache cache)
{
    private const int MaxTitle = 200;
    private const int MaxContent = 4000;
    private const int MaxIssueNo = 50;
    private const int MaxCompany = 100;

    public async Task<IssueImportResultDto> ImportAsync(
        IFormFile? file,
        long? currentUserMemberId,
        long? inProgressSubCategoryId,
        long? countersignSubCategoryId,
        long? doneSubCategoryId)
    {
        RequireFile(file);
        if (currentUserMemberId is not long memberId || memberId <= 0)
        {
            throw new AppException(400, "請先選擇目前使用者");
        }
        if (inProgressSubCategoryId is not long inProgressId || inProgressId <= 0
            || countersignSubCategoryId is not long countersignId || countersignId <= 0
            || doneSubCategoryId is not long doneId || doneId <= 0)
        {
            throw new AppException(400, "請選擇處理中、加簽與已結案小分類");
        }
        if (inProgressId == doneId || inProgressId == countersignId || doneId == countersignId)
        {
            throw new AppException(400, "處理中、加簽與已結案不可相同");
        }

        var member = await db.CompanyMembers.FirstOrDefaultAsync(x => x.CompanyMemberId == memberId)
            ?? throw new AppException(400, "請先選擇目前使用者");
        var chinese = member.MemberName.Trim();
        var english = member.EnglishName.Trim();
        if (chinese.Length == 0 && english.Length == 0)
        {
            throw new AppException(400, "請先選擇目前使用者");
        }

        var issueMajor = await db.MajorCategories.FirstOrDefaultAsync(x => x.CategoryName == "議題")
            ?? await db.MajorCategories.FirstOrDefaultAsync(x => x.CategoryName == "議題分類")
            ?? throw new AppException(400, "找不到大分類「議題」");
        var inProgressSub = await db.SubCategories.FirstOrDefaultAsync(x => x.SubCategoryId == inProgressId)
            ?? throw new AppException(400, "請選擇處理中、加簽與已結案小分類");
        var countersignSub = await db.SubCategories.FirstOrDefaultAsync(x => x.SubCategoryId == countersignId)
            ?? throw new AppException(400, "請選擇處理中、加簽與已結案小分類");
        var doneSub = await db.SubCategories.FirstOrDefaultAsync(x => x.SubCategoryId == doneId)
            ?? throw new AppException(400, "請選擇處理中、加簽與已結案小分類");
        if (inProgressSub.MajorCategoryId != issueMajor.MajorCategoryId
            || countersignSub.MajorCategoryId != issueMajor.MajorCategoryId
            || doneSub.MajorCategoryId != issueMajor.MajorCategoryId)
        {
            throw new AppException(400, "小分類不屬於大分類「議題」");
        }

        string html;
        await using (var stream = file!.OpenReadStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            html = await reader.ReadToEndAsync();
        }
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new AppException(400, "檔案不是議題匯入表格");
        }

        var rows = UofIssueHtmlParser.Parse(html);
        var summary = new IssueImportSummaryDto();
        var errors = new List<IssueImportRowErrorDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var valid = new List<ParsedIssue>();

        foreach (var row in rows)
        {
            if (!MatchesMember(row, chinese, english))
            {
                summary.Skipped++;
                continue;
            }
            var issueNo = Clip(row.IssueNo, MaxIssueNo);
            var title = Clip(row.Title, MaxTitle);
            if (issueNo.Length == 0)
            {
                errors.Add(Error(row.RowIndex, "missingIssueNo", "表單編號空白"));
                continue;
            }
            if (title.Length == 0)
            {
                errors.Add(Error(row.RowIndex, "missingTitle", "議題標題空白"));
                continue;
            }
            if (!seen.Add(issueNo))
            {
                errors.Add(Error(row.RowIndex, "duplicateIssueNo", "表單編號重複"));
                continue;
            }
            var vendor = Clip(UofIssueHtmlParser.ParseVendorName(row.VendorInfo), MaxCompany);
            if (vendor.Length == 0)
            {
                errors.Add(Error(row.RowIndex, "missingVendor", "廠商資訊空白"));
                continue;
            }
            if (!TryParseDue(row.DueDateText, out var due))
            {
                errors.Add(Error(row.RowIndex, "badDate", "預計完成日格式不正確"));
                continue;
            }
            valid.Add(new ParsedIssue
            {
                IssueNo = issueNo,
                Title = title,
                Content = Clip(row.Content, MaxContent),
                DueDate = due,
                VendorName = vendor,
                Status = row.Status,
                Closed = row.Status.Contains("結案", StringComparison.Ordinal),
                EngineerIsUser = IsEngineer(row, chinese, english),
                SignerIsUser = IsSigner(row, chinese, english)
            });
        }

        var companies = await db.ClientCompanies.ToListAsync();
        var createdEntities = new List<ClientCompany>();
        var issues = await db.Issues.Include(x => x.SubCategory).ToListAsync();
        var now = DateTime.Now;

        var pendingRows = new List<(IssueItem Item, string Status)>();
        IDbContextTransaction? tx = null;
        if (db.Database.IsRelational())
        {
            tx = await db.Database.BeginTransactionAsync();
        }
        try
        {
            foreach (var row in valid)
            {
                var company = ResolveCompany(companies, createdEntities, row.VendorName, now);
                var existing = issues.FirstOrDefault(x =>
                    string.Equals(x.IssueNo, row.IssueNo, StringComparison.OrdinalIgnoreCase));
                var subId = AssignSub(
                    existing,
                    row.Closed,
                    row.EngineerIsUser,
                    row.SignerIsUser,
                    inProgressId,
                    doneId,
                    issueMajor.MajorCategoryId);
                if (existing is null)
                {
                    var item = new IssueItem
                    {
                        IssueNo = row.IssueNo,
                        Title = row.Title,
                        Content = row.Content,
                        MajorCategoryId = issueMajor.MajorCategoryId,
                        SubCategoryId = subId,
                        DueDate = row.DueDate,
                        ClientCompany = company,
                        ImportedFromUof = true,
                        MissingKept = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    db.Issues.Add(item);
                    issues.Add(item);
                    summary.Created++;
                }
                else
                {
                    existing.IssueNo = row.IssueNo;
                    existing.Title = row.Title;
                    existing.Content = row.Content;
                    existing.DueDate = row.DueDate;
                    existing.ClientCompany = company;
                    existing.MajorCategoryId = issueMajor.MajorCategoryId;
                    existing.SubCategoryId = subId;
                    existing.ImportedFromUof = true;
                    existing.MissingKept = false;
                    existing.UpdatedAt = now;
                    summary.Updated++;
                    if (!row.Closed && row.EngineerIsUser && !row.SignerIsUser)
                    {
                        pendingRows.Add((existing, row.Status));
                    }
                }
            }

            await db.SaveChangesAsync();
            if (tx is not null)
            {
                await tx.CommitAsync();
            }
        }
        catch
        {
            if (tx is not null)
            {
                await tx.RollbackAsync();
            }
            throw;
        }
        finally
        {
            if (tx is not null)
            {
                await tx.DisposeAsync();
            }
        }

        var createdCompanies = createdEntities
            .Select(x => new IssueImportCreatedCompanyDto { Id = x.ClientCompanyId, Name = x.CompanyName })
            .ToList();

        var validNos = valid.Select(x => x.IssueNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notFound = issues
            .Where(x => x.IssueId > 0 && x.ImportedFromUof && !validNos.Contains(x.IssueNo))
            .OrderBy(x => x.IssueNo)
            .Select(x => new IssueImportNotFoundDto
            {
                Id = x.IssueId,
                IssueNo = x.IssueNo,
                Title = x.Title
            })
            .ToList();
        cache.RememberIssues(notFound.Select(x => x.Id));
        var pendingCategory = pendingRows
            .Where(x => x.Item.IssueId > 0)
            .OrderBy(x => x.Item.IssueNo)
            .Select(x => new IssueImportPendingCategoryDto
            {
                Id = x.Item.IssueId,
                IssueNo = x.Item.IssueNo,
                Title = x.Item.Title,
                Status = x.Status
            })
            .ToList();
        cache.RememberIssueCategories(pendingCategory.Select(x => x.Id), countersignId, doneId);

        return new IssueImportResultDto
        {
            Summary = summary,
            RowErrors = errors,
            CreatedCompanies = createdCompanies,
            NotFound = notFound,
            PendingCategory = pendingCategory,
            CountersignSubCategoryId = countersignId,
            DoneSubCategoryId = doneId
        };
    }

    public async Task ApplyDecisionsAsync(IssueImportDecisionRequestDto input)
    {
        var decisions = input.Decisions ?? [];
        var categoryChoices = input.CategoryChoices ?? [];
        if (decisions.Any(x => x.Id <= 0 || (x.Action ?? "").Trim() is not ("keep" or "delete")))
        {
            throw new AppException(400, "請為每筆未找到資料選擇保留或刪除");
        }
        if (categoryChoices.Any(x => x.Id <= 0 || (x.Action ?? "").Trim() is not ("countersign" or "done")))
        {
            throw new AppException(400, "請為每筆選擇加簽或結案");
        }
        if (cache.TryGetIssues(out var expected))
        {
            var incoming = new HashSet<long>();
            foreach (var item in decisions)
            {
                if (!incoming.Add(item.Id))
                {
                    throw new AppException(400, "請為每筆未找到資料選擇保留或刪除");
                }
            }
            if (!incoming.SetEquals(expected))
            {
                throw new AppException(400, "請為每筆未找到資料選擇保留或刪除");
            }
        }
        else if (decisions.Count == 0 && categoryChoices.Count == 0)
        {
            return;
        }

        long countersignId = 0;
        long doneId = 0;
        if (cache.TryGetIssueCategories(out var expectedCat, out countersignId, out doneId))
        {
            var incomingCat = new HashSet<long>();
            foreach (var item in categoryChoices)
            {
                if (!incomingCat.Add(item.Id))
                {
                    throw new AppException(400, "請為每筆選擇加簽或結案");
                }
            }
            if (!incomingCat.SetEquals(expectedCat))
            {
                throw new AppException(400, "請為每筆選擇加簽或結案");
            }
        }
        else if (categoryChoices.Count > 0)
        {
            countersignId = input.CountersignSubCategoryId ?? 0;
            doneId = input.DoneSubCategoryId ?? 0;
            if (countersignId <= 0 || doneId <= 0)
            {
                throw new AppException(400, "請重新匯入後再選擇加簽或結案");
            }
        }

        if (categoryChoices.Count > 0)
        {
            var catIds = categoryChoices.Select(x => x.Id).ToHashSet();
            var catRows = await db.Issues.Where(x => catIds.Contains(x.IssueId)).ToListAsync();
            if (catRows.Count != catIds.Count)
            {
                throw new AppException(400, "請為每筆選擇加簽或結案");
            }
            var now = DateTime.Now;
            foreach (var choice in categoryChoices)
            {
                var row = catRows.First(x => x.IssueId == choice.Id);
                row.SubCategoryId = choice.Action == "done" ? doneId : countersignId;
                row.UpdatedAt = now;
            }
        }

        if (decisions.Count == 0)
        {
            await db.SaveChangesAsync();
            return;
        }

        var ids = decisions.Select(x => x.Id).ToHashSet();
        var rows = await db.Issues.Where(x => ids.Contains(x.IssueId)).ToListAsync();
        if (rows.Count != ids.Count)
        {
            throw new AppException(400, "請為每筆未找到資料選擇保留或刪除");
        }

        foreach (var decision in decisions)
        {
            var row = rows.First(x => x.IssueId == decision.Id);
            if (decision.Action == "keep")
            {
                row.MissingKept = true;
                row.UpdatedAt = DateTime.Now;
            }
            else
            {
                db.TrackTodos.RemoveRange(db.TrackTodos.Where(x => x.IssueId == row.IssueId));
                db.IssueTodos.RemoveRange(db.IssueTodos.Where(x => x.IssueId == row.IssueId));
                db.IssuePlans.RemoveRange(db.IssuePlans.Where(x => x.IssueId == row.IssueId));
                db.Issues.Remove(row);
            }
        }
        await db.SaveChangesAsync();
    }

    private static long? AssignSub(
        IssueItem? existing,
        bool closed,
        bool engineerIsUser,
        bool signerIsUser,
        long inProgressId,
        long doneId,
        long issueMajorId)
    {
        if (closed) return doneId;
        if (engineerIsUser)
        {
            if (existing is null) return inProgressId;
            if (!signerIsUser) return existing.SubCategoryId;
            return inProgressId;
        }
        if (existing is null) return inProgressId;
        var belongs = existing.SubCategoryId is long
            && existing.SubCategory is not null
            && existing.SubCategory.MajorCategoryId == issueMajorId;
        return belongs ? existing.SubCategoryId : inProgressId;
    }

    private ClientCompany ResolveCompany(
        List<ClientCompany> companies,
        List<ClientCompany> created,
        string name,
        DateTime now)
    {
        var existing = companies.FirstOrDefault(x =>
            string.Equals(x.CompanyName, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        var item = new ClientCompany
        {
            CompanyName = name,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.ClientCompanies.Add(item);
        companies.Add(item);
        created.Add(item);
        return item;
    }

    private static bool MatchesMember(UofIssueRow row, string chinese, string english) =>
        HitsName(row.Sa, chinese, english)
        || IsEngineer(row, chinese, english);

    private static bool IsEngineer(UofIssueRow row, string chinese, string english) =>
        HitsName(row.Engineer1, chinese, english) || HitsName(row.Engineer2, chinese, english);

    private static bool IsSigner(UofIssueRow row, string chinese, string english) =>
        HitsName(row.CurrentApprover, chinese, english);

    private static bool HitsName(string cell, string chinese, string english) =>
        ContainsName(cell, chinese) || ContainsName(cell, english);

    private static bool ContainsName(string cell, string name) =>
        name.Length > 0 && cell.Contains(name, StringComparison.OrdinalIgnoreCase);

    private static void RequireFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw new AppException(400, "請選擇一個檔案");
        }
        var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
        if (ext is not (".xls" or ".html" or ".htm"))
        {
            throw new AppException(400, "僅支援 xls 或 html");
        }
    }

    private static bool TryParseDue(string text, out DateOnly? due)
    {
        due = null;
        var value = (text ?? "").Trim();
        if (value.Length == 0) return true;
        string[] formats = ["yyyy/M/d", "yyyy-M-d", "yyyy/MM/dd", "yyyy-MM-dd"];
        if (DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            due = parsed;
            return true;
        }
        return false;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? "").Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static IssueImportRowErrorDto Error(int rowIndex, string kind, string message) => new()
    {
        RowIndex = rowIndex,
        Kind = kind,
        Message = message
    };

    private sealed class ParsedIssue
    {
        public string IssueNo { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public DateOnly? DueDate { get; set; }
        public string VendorName { get; set; } = "";
        public string Status { get; set; } = "";
        public bool Closed { get; set; }
        public bool EngineerIsUser { get; set; }
        public bool SignerIsUser { get; set; }
    }
}
