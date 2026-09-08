using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Issue.Api.Common;
using Issue.Api.Data;
using Issue.Api.Dtos;
using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Services;

public class ProjectImportService(AppDbContext db, ImportNotFoundCache cache)
{
    public const string OwnerWorkItem = "workItem";
    public const string OwnerIssue = "projectIssue";
    private static readonly Regex RemarkHour = new(
        @"(?:(?<y>\d{4})[/\-])?(?<m>\d{1,2})[/\-](?<d>\d{1,2})\s*-?\s*(?<h>\d+(?:\.\d+)?)\s*[Hh]",
        RegexOptions.CultureInvariant);

    public async Task<ResolveProjectDto> ResolveProjectAsync(IFormFile? file)
    {
        var name = RequireXlsx(file);
        if (!TryParseFileName(name, out var code, out var suggested))
        {
            throw new AppException(400, "檔名解析不出專案代號");
        }
        await using var stream = file!.OpenReadStream();
        try
        {
            using var _ = new XLWorkbook(stream);
        }
        catch
        {
            throw new AppException(400, "僅支援 xlsx");
        }

        var project = await db.Projects.FirstOrDefaultAsync(x => x.ProjectCode.ToLower() == code.ToLower());
        if (project is null)
        {
            return new ResolveProjectDto
            {
                Status = "needProject",
                ProjectCode = code,
                SuggestedName = suggested
            };
        }
        return new ResolveProjectDto
        {
            Status = "ready",
            ProjectId = project.ProjectId,
            ProjectCode = project.ProjectCode,
            SuggestedName = suggested
        };
    }

    public async Task<ImportResultDto> ImportAsync(long projectId, IFormFile? file, long? currentUserMemberId)
    {
        var project = await db.Projects.FirstOrDefaultAsync(x => x.ProjectId == projectId)
            ?? throw new AppException(404, "找不到該專案");
        RequireXlsx(file);
        if (currentUserMemberId is not long memberId || memberId <= 0)
        {
            throw new AppException(400, "請先選擇目前使用者");
        }
        var member = await db.CompanyMembers.FirstOrDefaultAsync(x => x.CompanyMemberId == memberId)
            ?? throw new AppException(400, "找不到該公司成員");
        var memberName = member.MemberName.Trim();
        var englishName = member.EnglishName.Trim();
        if (memberName.Length == 0)
        {
            throw new AppException(400, "請先選擇目前使用者");
        }

        await using var stream = file!.OpenReadStream();
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch
        {
            throw new AppException(400, "僅支援 xlsx");
        }

        using (workbook)
        {
            var workSheet = FindSheet(workbook, "工作項目")
                ?? throw new AppException(400, "找不到工作表「工作項目」");
            var issueSheet = FindSheet(workbook, "議題單")
                ?? throw new AppException(400, "找不到工作表「議題單」");

            var workRows = ParseWorkItems(workSheet, memberName, englishName);
            if (workRows.Valid.Count == 0)
            {
                throw new AppException(400, "沒有符合目前使用者的工作項次");
            }
            var issueRows = ParseIssues(issueSheet, memberName, englishName);
            var now = DateTime.Now;
            var majorId = await DefaultMajorId();

            var existingItems = await db.ProjectWorkItems.Where(x => x.ProjectId == projectId).ToListAsync();
            var existingIssues = await db.ProjectIssues.Where(x => x.ProjectId == projectId).ToListAsync();
            var seenItem = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenIssue = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var summary = new ImportSummaryDto();
            var importedItems = new List<(ProjectWorkItem Row, string Remark)>();
            var importedIssues = new List<(ProjectIssue Row, string Remark)>();

            foreach (var row in workRows.Valid)
            {
                var key = row.Code.ToLowerInvariant();
                if (!seenItem.Add(key))
                {
                    workRows.Errors.Add(new ImportRowErrorDto
                    {
                        Sheet = "工作項目",
                        RowIndex = row.RowIndex,
                        Kind = "duplicateWorkItemCode",
                        Message = $"工作代號「{row.Code}」在檔內重複"
                    });
                    summary.WorkItemSkipped += 1;
                    continue;
                }
                var found = existingItems.FirstOrDefault(x => x.WorkItemCode.Equals(row.Code, StringComparison.OrdinalIgnoreCase));
                if (found is null)
                {
                    found = new ProjectWorkItem
                    {
                        ProjectId = projectId,
                        WorkItemCode = row.Code,
                        CreatedAt = now
                    };
                    db.ProjectWorkItems.Add(found);
                    existingItems.Add(found);
                    summary.WorkItemCreated += 1;
                }
                else
                {
                    summary.WorkItemUpdated += 1;
                }
                found.Title = row.Title;
                found.PlannedDays = row.PlannedDays;
                found.OwnerName = row.OwnerName;
                found.StartDate = row.StartDate;
                found.DueDate = row.DueDate;
                if (row.ActualEndDate is DateOnly actualEnd)
                {
                    found.ActualEndDate = actualEnd;
                    found.IsCompleted = true;
                }
                found.MissingKept = false;
                found.UpdatedAt = now;
                importedItems.Add((found, row.Remark));
            }

            foreach (var row in issueRows.Valid)
            {
                var key = row.SeqNo.ToLowerInvariant();
                if (!seenIssue.Add(key))
                {
                    issueRows.Errors.Add(new ImportRowErrorDto
                    {
                        Sheet = "議題單",
                        RowIndex = row.RowIndex,
                        Kind = "duplicateSeqNo",
                        Message = $"項次「{row.SeqNo}」在檔內重複"
                    });
                    summary.IssueSkipped += 1;
                    continue;
                }
                var found = existingIssues.FirstOrDefault(x => x.SeqNo.Equals(row.SeqNo, StringComparison.OrdinalIgnoreCase));
                if (found is null)
                {
                    found = new ProjectIssue
                    {
                        ProjectId = projectId,
                        SeqNo = row.SeqNo,
                        MajorCategoryId = majorId,
                        CreatedAt = now
                    };
                    db.ProjectIssues.Add(found);
                    existingIssues.Add(found);
                    summary.IssueCreated += 1;
                }
                else
                {
                    summary.IssueUpdated += 1;
                }
                found.Title = row.Title;
                found.Content = row.Content;
                found.HandlerName = row.HandlerName;
                found.DueDate = row.DueDate;
                if (row.ActualEndDate is DateOnly actualEnd)
                {
                    found.ActualEndDate = actualEnd;
                    found.IsCompleted = true;
                }
                found.MissingKept = false;
                found.UpdatedAt = now;
                importedIssues.Add((found, row.Remark));
            }

            await db.SaveChangesAsync();

            var hourDiffs = new List<ImportHourDiffDto>();
            var hourReviews = new List<ImportHourReviewDto>();
            foreach (var (row, remark) in importedItems)
            {
                hourDiffs.AddRange(await SyncRemarkHours(OwnerWorkItem, row.ProjectWorkItemId, LabelWorkItem(row), remark));
                AddHourReview(hourReviews, hourDiffs, OwnerWorkItem, row.ProjectWorkItemId, LabelWorkItem(row), remark, row.Remark);
            }
            foreach (var (row, remark) in importedIssues)
            {
                hourDiffs.AddRange(await SyncRemarkHours(OwnerIssue, row.ProjectIssueId, LabelIssue(row), remark));
                AddHourReview(hourReviews, hourDiffs, OwnerIssue, row.ProjectIssueId, LabelIssue(row), remark, row.Remark);
            }

            var notFound = existingItems
                .Where(x => !seenItem.Contains(x.WorkItemCode.ToLowerInvariant()))
                .Select(x => new ImportNotFoundDto
                {
                    OwnerKind = OwnerWorkItem,
                    OwnerId = x.ProjectWorkItemId,
                    OwnerLabel = LabelWorkItem(x)
                })
                .Concat(existingIssues
                    .Where(x => !seenIssue.Contains(x.SeqNo.ToLowerInvariant()))
                    .Select(x => new ImportNotFoundDto
                    {
                        OwnerKind = OwnerIssue,
                        OwnerId = x.ProjectIssueId,
                        OwnerLabel = LabelIssue(x)
                    }))
                .ToList();

            cache.RememberProject(projectId, notFound.Select(x => (x.OwnerKind, x.OwnerId)));
            return new ImportResultDto
            {
                ProjectId = projectId,
                Summary = summary,
                RowErrors = workRows.Errors.Concat(issueRows.Errors).ToList(),
                HourDiffs = hourDiffs,
                HourReviews = hourReviews,
                NotFound = notFound
            };
        }
    }

    public async Task ApplyDecisionsAsync(long projectId, ImportDecisionRequestDto input)
    {
        if (!await db.Projects.AnyAsync(x => x.ProjectId == projectId))
        {
            throw new AppException(404, "找不到該專案");
        }
        var decisions = input.Decisions ?? [];
        if (cache.TryGetProject(projectId, out var expected))
        {
            var incoming = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in decisions)
            {
                var key = ImportNotFoundCache.Key((item.OwnerKind ?? "").Trim(), item.OwnerId);
                if (!incoming.Add(key))
                {
                    throw new AppException(400, "請為每筆未找到資料選擇保留或刪除");
                }
            }
            if (!incoming.SetEquals(expected))
            {
                throw new AppException(400, "請為每筆未找到資料選擇保留或刪除");
            }
        }
        else if (decisions.Count == 0)
        {
            return;
        }
        if (decisions.Any(x =>
                (x.Action ?? "").Trim() is not ("keep" or "delete")
                || (x.OwnerKind ?? "") is not (OwnerWorkItem or OwnerIssue)
                || x.OwnerId <= 0))
        {
            throw new AppException(400, "請為每筆未找到資料選擇保留或刪除");
        }

        var itemIds = decisions.Where(x => x.OwnerKind == OwnerWorkItem).Select(x => x.OwnerId).ToHashSet();
        var issueIds = decisions.Where(x => x.OwnerKind == OwnerIssue).Select(x => x.OwnerId).ToHashSet();
        var items = await db.ProjectWorkItems.Where(x => x.ProjectId == projectId && itemIds.Contains(x.ProjectWorkItemId)).ToListAsync();
        var issues = await db.ProjectIssues.Where(x => x.ProjectId == projectId && issueIds.Contains(x.ProjectIssueId)).ToListAsync();
        if (items.Count != itemIds.Count || issues.Count != issueIds.Count)
        {
            throw new AppException(400, "請為每筆未找到資料選擇保留或刪除");
        }

        foreach (var decision in decisions)
        {
            if (decision.OwnerKind == OwnerWorkItem)
            {
                var row = items.First(x => x.ProjectWorkItemId == decision.OwnerId);
                if (decision.Action == "keep")
                {
                    row.MissingKept = true;
                    row.UpdatedAt = DateTime.Now;
                }
                else
                {
                    db.TrackTodos.RemoveRange(db.TrackTodos.Where(x => x.ProjectWorkItemId == row.ProjectWorkItemId));
                    db.WorkHours.RemoveRange(db.WorkHours.Where(x => x.ProjectWorkItemId == row.ProjectWorkItemId));
                    db.ProjectWorkItems.Remove(row);
                }
            }
            else
            {
                var row = issues.First(x => x.ProjectIssueId == decision.OwnerId);
                if (decision.Action == "keep")
                {
                    row.MissingKept = true;
                    row.UpdatedAt = DateTime.Now;
                }
                else
                {
                    db.TrackTodos.RemoveRange(db.TrackTodos.Where(x => x.ProjectIssueId == row.ProjectIssueId));
                    db.WorkHours.RemoveRange(db.WorkHours.Where(x => x.ProjectIssueId == row.ProjectIssueId));
                    db.ProjectIssues.Remove(row);
                }
            }
        }
        await db.SaveChangesAsync();
    }

    public static bool TryParseFileName(string? fileName, out string projectCode, out string suggestedName)
    {
        projectCode = "";
        suggestedName = "";
        var stem = Path.GetFileNameWithoutExtension(fileName ?? "").Trim();
        if (stem.Length == 0)
        {
            return false;
        }
        var space = stem.IndexOf(' ');
        if (space < 0)
        {
            projectCode = stem;
            return projectCode.Length is > 0 and <= 50;
        }
        projectCode = stem[..space].Trim();
        suggestedName = stem[(space + 1)..].Trim();
        return projectCode.Length is > 0 and <= 50;
    }

    public static bool PassesOwnerFilter(string? cell, params string?[] memberNames)
    {
        var text = (cell ?? "").Trim();
        if (text.Length == 0)
        {
            return false;
        }
        if (text.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        foreach (var raw in memberNames)
        {
            var name = (raw ?? "").Trim();
            if (name.Length > 0 && text.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static List<ImportRemarkLineDto> ParseRemarkLines(string? remark, int year)
    {
        var result = new List<ImportRemarkLineDto>();
        var index = 0;
        foreach (var raw in (remark ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var text = raw.Trim();
            if (text.Length == 0)
            {
                continue;
            }
            var line = new ImportRemarkLineDto { Index = index, Text = text, Kind = "text" };
            var match = RemarkHour.Match(text);
            if (match.Success)
            {
                var y = match.Groups["y"].Success ? int.Parse(match.Groups["y"].Value) : year;
                var m = int.Parse(match.Groups["m"].Value);
                var d = int.Parse(match.Groups["d"].Value);
                if (DateOnly.TryParse(string.Create(CultureInfo.InvariantCulture, $"{y:0000}-{m:00}-{d:00}"), out var date)
                    && decimal.TryParse(match.Groups["h"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var hours)
                    && hours > 0)
                {
                    line.Kind = "hour";
                    line.Date = date;
                    line.Hours = decimal.Round(hours, 2);
                }
            }
            result.Add(line);
            index += 1;
        }
        return result;
    }

    public static List<(DateOnly Date, decimal Hours)> ParseRemarkHours(string? remark, int year) =>
        ParseRemarkLines(remark, year)
            .Where(x => x.Kind == "hour" && x.Date != null && x.Hours != null)
            .Select(x => (x.Date!.Value, x.Hours!.Value))
            .ToList();

    public static List<string> UnparseableRemarkLines(string? remark)
    {
        var lines = new List<string>();
        foreach (var raw in (remark ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }
            var hasHourLetter = line.Contains('H', StringComparison.OrdinalIgnoreCase);
            var hasDate = Regex.IsMatch(line, @"\d{1,2}\s*[/\-]\s*\d{1,2}");
            if (hasHourLetter && hasDate && !RemarkHour.IsMatch(line))
            {
                lines.Add(line);
            }
        }
        return lines;
    }

    public static bool LooksUnparseable(string? remark) => UnparseableRemarkLines(remark).Count > 0;

    public static bool RemarkContainsLine(string? ownerRemark, string? line)
    {
        var text = (line ?? "").Trim();
        if (text.Length == 0)
        {
            return false;
        }
        var owner = (ownerRemark ?? "").Replace("\r\n", "\n");
        foreach (var raw in owner.Split('\n'))
        {
            if (raw.Trim().Equals(text, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return owner.Contains(text, StringComparison.Ordinal);
    }

    private static void AddHourReview(
        List<ImportHourReviewDto> reviews,
        List<ImportHourDiffDto> diffs,
        string ownerKind,
        long ownerId,
        string label,
        string excelRemark,
        string? ownerRemark)
    {
        var lines = ParseRemarkLines(excelRemark, DateTime.Now.Year);
        if (lines.Count == 0)
        {
            return;
        }
        var pending = lines
            .Where(x => x.Kind != "text" || !RemarkContainsLine(ownerRemark, x.Text))
            .ToList();
        var hasText = pending.Any(x => x.Kind == "text");
        var hasDiff = diffs.Any(x => x.OwnerKind == ownerKind && x.OwnerId == ownerId);
        if (!hasText && !hasDiff)
        {
            return;
        }
        reviews.Add(new ImportHourReviewDto
        {
            OwnerKind = ownerKind,
            OwnerId = ownerId,
            OwnerLabel = label,
            Remark = excelRemark ?? "",
            Lines = pending
        });
    }

    private async Task<List<ImportHourDiffDto>> SyncRemarkHours(string ownerKind, long ownerId, string label, string remark)
    {
        await WriteMissingRemarkHours(ownerKind, ownerId, remark);
        return await DiffHours(ownerKind, ownerId, label, remark);
    }

    private async Task WriteMissingRemarkHours(string ownerKind, long ownerId, string remark)
    {
        var excel = ParseRemarkHours(remark, DateTime.Now.Year)
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.Last().Hours);
        if (excel.Count == 0)
        {
            return;
        }
        var existing = ownerKind == OwnerWorkItem
            ? await db.WorkHours.Where(x => x.ProjectWorkItemId == ownerId).ToListAsync()
            : await db.WorkHours.Where(x => x.ProjectIssueId == ownerId).ToListAsync();
        var have = existing.Select(x => x.WorkDate).ToHashSet();
        var added = false;
        foreach (var (date, hours) in excel)
        {
            if (have.Contains(date) || date.Year is < 2000 or > 2100 || hours <= 0 || hours > 999.99m)
            {
                continue;
            }
            db.WorkHours.Add(new WorkHour
            {
                ProjectWorkItemId = ownerKind == OwnerWorkItem ? ownerId : null,
                ProjectIssueId = ownerKind == OwnerIssue ? ownerId : null,
                WorkDate = date,
                HourValue = hours,
                Remark = ""
            });
            added = true;
        }
        if (!added)
        {
            return;
        }
        await db.SaveChangesAsync();
        var dates = await (ownerKind == OwnerWorkItem
            ? db.WorkHours.Where(x => x.ProjectWorkItemId == ownerId)
            : db.WorkHours.Where(x => x.ProjectIssueId == ownerId)).Select(x => x.WorkDate).ToListAsync();
        var start = dates.Count == 0 ? (DateOnly?)null : dates.Min();
        if (ownerKind == OwnerWorkItem)
        {
            var row = await db.ProjectWorkItems.FirstAsync(x => x.ProjectWorkItemId == ownerId);
            row.ActualStartDate = start;
            row.UpdatedAt = DateTime.Now;
        }
        else
        {
            var row = await db.ProjectIssues.FirstAsync(x => x.ProjectIssueId == ownerId);
            row.ActualStartDate = start;
            row.UpdatedAt = DateTime.Now;
        }
        await db.SaveChangesAsync();
    }

    private async Task<List<ImportHourDiffDto>> DiffHours(string ownerKind, long ownerId, string label, string remark)
    {
        var diffs = new List<ImportHourDiffDto>();
        var badLines = UnparseableRemarkLines(remark);
        if (badLines.Count > 0)
        {
            diffs.Add(new ImportHourDiffDto
            {
                OwnerKind = ownerKind,
                OwnerId = ownerId,
                OwnerLabel = label,
                Kind = "unparseableRemark",
                Detail = string.Join("\n", badLines),
                Remark = remark ?? ""
            });
        }
        var excel = ParseRemarkHours(remark, DateTime.Now.Year)
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.Last().Hours);
        var webRows = ownerKind == OwnerWorkItem
            ? await db.WorkHours.Where(x => x.ProjectWorkItemId == ownerId).ToListAsync()
            : await db.WorkHours.Where(x => x.ProjectIssueId == ownerId).ToListAsync();
        var web = webRows.ToDictionary(x => x.WorkDate, x => x.HourValue);
        foreach (var (date, hours) in excel)
        {
            if (!web.TryGetValue(date, out var webHours))
            {
                diffs.Add(Diff(ownerKind, ownerId, label, "hoursExcelOnly", date, hours, null, remark ?? ""));
            }
            else if (webHours != hours)
            {
                diffs.Add(Diff(ownerKind, ownerId, label, "hoursDiffer", date, hours, webHours, remark ?? ""));
            }
        }
        foreach (var (date, hours) in web)
        {
            if (!excel.ContainsKey(date))
            {
                diffs.Add(Diff(ownerKind, ownerId, label, "hoursWebOnly", date, null, hours, remark ?? ""));
            }
        }
        return diffs;
    }

    private static ImportHourDiffDto Diff(
        string ownerKind, long ownerId, string label, string kind, DateOnly? date, decimal? excel, decimal? web, string remark = "") => new()
    {
        OwnerKind = ownerKind,
        OwnerId = ownerId,
        OwnerLabel = label,
        Kind = kind,
        Date = date,
        ExcelHours = excel,
        WebHours = web,
        Remark = remark ?? ""
    };

    private static ParsedSheet<WorkRow> ParseWorkItems(IXLWorksheet sheet, params string?[] memberNames)
    {
        var map = FindHeaders(sheet, ["工作代號", "工作說明", "計畫人天", "計劃人天", "負責人員", "預計開始日", "預計開始", "預計完成日", "預計完成", "實際完成日", "實際結束日", "實際結束", "備註"]);
        var result = new ParsedSheet<WorkRow>();
        if (map.HeaderRow == 0 || !map.Columns.ContainsKey("工作代號"))
        {
            return result;
        }
        var last = sheet.LastRowUsed()?.RowNumber() ?? map.HeaderRow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var r = map.HeaderRow + 1; r <= last; r++)
        {
            var code = CellText(sheet.Cell(r, map.Columns["工作代號"]));
            var owner = CellText(sheet.Cell(r, Col(map, "負責人員")));
            if (string.IsNullOrWhiteSpace(code))
            {
                if (HasAnyValue(sheet, r, map))
                {
                    result.Errors.Add(new ImportRowErrorDto
                    {
                        Sheet = "工作項目",
                        RowIndex = r,
                        Kind = "missingWorkItemCode",
                        Message = "工作代號不可空白"
                    });
                }
                continue;
            }
            if (!PassesOwnerFilter(owner, memberNames))
            {
                continue;
            }
            if (!seen.Add(code))
            {
                result.Errors.Add(new ImportRowErrorDto
                {
                    Sheet = "工作項目",
                    RowIndex = r,
                    Kind = "duplicateWorkItemCode",
                    Message = $"工作代號「{code}」在檔內重複"
                });
                continue;
            }
            if (code.Length > 50)
            {
                result.Errors.Add(new ImportRowErrorDto
                {
                    Sheet = "工作項目",
                    RowIndex = r,
                    Kind = "missingWorkItemCode",
                    Message = "工作代號過長"
                });
                continue;
            }
            result.Valid.Add(new WorkRow
            {
                RowIndex = r,
                Code = code,
                Title = Truncate(CellText(CellAt(sheet, r, Col(map, "工作說明"))), 4000),
                PlannedDays = ParseDays(CellAt(sheet, r, Col(map, "計畫人天", "計劃人天"))),
                OwnerName = Truncate(owner, 200),
                StartDate = CellDate(CellAt(sheet, r, Col(map, "預計開始日", "預計開始"))),
                DueDate = CellDate(CellAt(sheet, r, Col(map, "預計完成日", "預計完成"))),
                ActualEndDate = CellDate(CellAt(sheet, r, Col(map, "實際完成日", "實際結束日", "實際結束"))),
                Remark = CellText(CellAt(sheet, r, Col(map, "備註")))
            });
        }
        return result;
    }

    private static ParsedSheet<IssueRow> ParseIssues(IXLWorksheet sheet, params string?[] memberNames)
    {
        var map = FindHeaders(sheet, ["項次", "需求說明", "解決方案", "處理人員", "預計完成日", "預計完成", "實際完成日", "實際結束日", "實際結束", "備註"]);
        var result = new ParsedSheet<IssueRow>();
        if (map.HeaderRow == 0 || !map.Columns.ContainsKey("項次"))
        {
            return result;
        }
        var last = sheet.LastRowUsed()?.RowNumber() ?? map.HeaderRow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var r = map.HeaderRow + 1; r <= last; r++)
        {
            var seq = CellText(sheet.Cell(r, map.Columns["項次"]));
            var handler = CellText(sheet.Cell(r, Col(map, "處理人員")));
            if (string.IsNullOrWhiteSpace(seq))
            {
                if (HasAnyValue(sheet, r, map))
                {
                    result.Errors.Add(new ImportRowErrorDto
                    {
                        Sheet = "議題單",
                        RowIndex = r,
                        Kind = "missingSeqNo",
                        Message = "項次不可空白"
                    });
                }
                continue;
            }
            if (!PassesOwnerFilter(handler, memberNames))
            {
                continue;
            }
            if (!seen.Add(seq))
            {
                result.Errors.Add(new ImportRowErrorDto
                {
                    Sheet = "議題單",
                    RowIndex = r,
                    Kind = "duplicateSeqNo",
                    Message = $"項次「{seq}」在檔內重複"
                });
                continue;
            }
            result.Valid.Add(new IssueRow
            {
                RowIndex = r,
                SeqNo = Truncate(seq, 50),
                Title = Truncate(CellText(CellAt(sheet, r, Col(map, "需求說明"))), 200) is { Length: > 0 } title ? title : seq,
                Content = Truncate(CellText(CellAt(sheet, r, Col(map, "解決方案"))), 4000),
                HandlerName = Truncate(handler, 50),
                DueDate = CellDate(CellAt(sheet, r, Col(map, "預計完成日", "預計完成"))),
                ActualEndDate = CellDate(CellAt(sheet, r, Col(map, "實際完成日", "實際結束日", "實際結束"))),
                Remark = CellText(CellAt(sheet, r, Col(map, "備註")))
            });
        }
        return result;
    }

    private async Task<long> DefaultMajorId()
    {
        var preferred = await db.MajorCategories.FirstOrDefaultAsync(x => x.CategoryName == "議題分類");
        if (preferred is not null)
        {
            return preferred.MajorCategoryId;
        }
        var first = await db.MajorCategories.OrderBy(x => x.SortOrder).FirstOrDefaultAsync()
            ?? throw new AppException(400, "無效的大分類");
        return first.MajorCategoryId;
    }

    private static string RequireXlsx(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw new AppException(400, "請選擇一個 Excel 檔");
        }
        var name = file.FileName ?? "";
        if (!name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException(400, "僅支援 xlsx");
        }
        return name;
    }

    private static IXLWorksheet? FindSheet(XLWorkbook workbook, string name) =>
        workbook.Worksheets.FirstOrDefault(x => x.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));

    private static HeaderMap FindHeaders(IXLWorksheet sheet, string[] names)
    {
        var map = new HeaderMap();
        var last = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 1, 20);
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 1;
        for (var r = 1; r <= last; r++)
        {
            var found = 0;
            var cols = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var c = 1; c <= lastCol; c++)
            {
                var text = CellText(sheet.Cell(r, c));
                if (text.Length == 0)
                {
                    continue;
                }
                var match = names.FirstOrDefault(n => text.Equals(n, StringComparison.OrdinalIgnoreCase)
                    || (n.Contains("處理時間") && text.Contains("處理時間", StringComparison.OrdinalIgnoreCase)));
                if (match is null && names.Contains(text))
                {
                    match = text;
                }
                if (match is not null && !cols.ContainsKey(match))
                {
                    cols[match] = c;
                    found += 1;
                }
            }
            if (found >= 2)
            {
                map.HeaderRow = r;
                map.Columns = cols;
                break;
            }
        }
        return map;
    }

    private static int Col(HeaderMap map, params string[] names)
    {
        foreach (var name in names)
        {
            if (map.Columns.TryGetValue(name, out var col))
            {
                return col;
            }
        }
        return 0;
    }

    private static bool HasAnyValue(IXLWorksheet sheet, int row, HeaderMap map) =>
        map.Columns.Values.Any(c => c > 0 && CellText(CellAt(sheet, row, c)).Length > 0);

    private static IXLCell? CellAt(IXLWorksheet sheet, int row, int col) =>
        col > 0 ? sheet.Cell(row, col) : null;

    private static string CellText(IXLCell? cell)
    {
        if (cell is null || cell.IsEmpty())
        {
            return "";
        }
        try
        {
            if (cell.DataType == XLDataType.DateTime)
            {
                return cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            if (cell.DataType == XLDataType.Number)
            {
                var number = cell.GetDouble();
                return Math.Abs(number - Math.Round(number)) < 0.0001
                    ? ((long)Math.Round(number)).ToString(CultureInfo.InvariantCulture)
                    : number.ToString(CultureInfo.InvariantCulture);
            }
            return cell.GetString().Trim();
        }
        catch
        {
            return cell.GetFormattedString().Trim();
        }
    }

    private static DateOnly? CellDate(IXLCell? cell)
    {
        if (cell is null || cell.IsEmpty() || cell.Address.ColumnNumber == 0)
        {
            return null;
        }
        try
        {
            if (cell.DataType == XLDataType.DateTime)
            {
                return DateOnly.FromDateTime(cell.GetDateTime());
            }
            if (cell.DataType == XLDataType.Number)
            {
                return DateOnly.FromDateTime(DateTime.FromOADate(cell.GetDouble()));
            }
            var text = cell.GetString().Trim();
            if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                || DateOnly.TryParse(text, new CultureInfo("zh-TW"), DateTimeStyles.None, out d))
            {
                return d;
            }
        }
        catch
        {
            return null;
        }
        return null;
    }

    private static decimal? ParseDays(IXLCell? cell)
    {
        if (cell is null || cell.IsEmpty() || cell.Address.ColumnNumber == 0)
        {
            return null;
        }
        try
        {
            if (cell.DataType == XLDataType.Number)
            {
                var value = Convert.ToDecimal(cell.GetDouble());
                return value < 0 ? null : decimal.Round(value, 2);
            }
            if (decimal.TryParse(cell.GetString().Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                && parsed >= 0)
            {
                return decimal.Round(parsed, 2);
            }
        }
        catch
        {
            return null;
        }
        return null;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string LabelWorkItem(ProjectWorkItem x) =>
        string.IsNullOrWhiteSpace(x.Title) ? x.WorkItemCode : $"{x.WorkItemCode} {x.Title}";

    private static string LabelIssue(ProjectIssue x) =>
        string.IsNullOrWhiteSpace(x.Title) ? x.SeqNo : $"{x.SeqNo} {x.Title}";

    private sealed class HeaderMap
    {
        public int HeaderRow { get; set; }
        public Dictionary<string, int> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ParsedSheet<T>
    {
        public List<T> Valid { get; } = [];
        public List<ImportRowErrorDto> Errors { get; } = [];
    }

    private sealed class WorkRow
    {
        public int RowIndex { get; set; }
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public decimal? PlannedDays { get; set; }
        public string OwnerName { get; set; } = "";
        public DateOnly? StartDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public DateOnly? ActualEndDate { get; set; }
        public string Remark { get; set; } = "";
    }

    private sealed class IssueRow
    {
        public int RowIndex { get; set; }
        public string SeqNo { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string HandlerName { get; set; } = "";
        public DateOnly? DueDate { get; set; }
        public DateOnly? ActualEndDate { get; set; }
        public string Remark { get; set; } = "";
    }
}
