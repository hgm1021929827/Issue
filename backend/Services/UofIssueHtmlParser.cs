using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Issue.Api.Common;

namespace Issue.Api.Services;

public static class UofIssueHtmlParser
{
    public const string ColIssueNo = "表單編號";
    public const string ColTitle = "議題標題";
    public const string ColContent = "議題內容";
    public const string ColDueDate = "預計完成日";
    public const string ColVendor = "廠商資訊";
    public const string ColStatus = "狀態";
    public const string ColApprover = "目前簽核者";
    public const string ColSa = "SA";
    public const string ColEng1 = "工程師-1";
    public const string ColEng2 = "工程師-2";

    public static List<UofIssueRow> Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html ?? "");
        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables is null || tables.Count == 0)
        {
            throw new AppException(400, "檔案不是議題匯入表格");
        }

        HtmlNode? target = null;
        Dictionary<string, int>? map = null;
        var sawIssueNo = false;
        foreach (var table in tables)
        {
            var rows = table.SelectNodes("./tr|./tbody/tr|./thead/tr");
            if (rows is null || rows.Count == 0) continue;
            var headers = RowCells(rows[0]).Select(node => DecodeHeader(CellText(node))).ToList();
            if (headers.Contains(ColIssueNo)) sawIssueNo = true;
            if (!headers.Contains(ColIssueNo) || !headers.Contains(ColTitle)) continue;
            map = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < headers.Count; i++)
            {
                var name = headers[i];
                if (name.Length > 0 && !map.ContainsKey(name)) map[name] = i;
            }
            target = table;
            break;
        }

        if (target is null || map is null)
        {
            throw new AppException(400, sawIssueNo ? "檔案不是議題匯入表格" : "找不到表頭「表單編號」");
        }

        var allRows = target.SelectNodes("./tr|./tbody/tr|./thead/tr");
        var list = new List<UofIssueRow>();
        if (allRows is null) return list;
        var bodyIndex = 0;
        foreach (var tr in allRows.Skip(1))
        {
            bodyIndex++;
            var cells = RowCells(tr).Select(CellText).ToList();
            list.Add(new UofIssueRow
            {
                RowIndex = bodyIndex,
                IssueNo = Cell(cells, map, ColIssueNo),
                Title = Cell(cells, map, ColTitle),
                Content = Cell(cells, map, ColContent),
                DueDateText = Cell(cells, map, ColDueDate),
                VendorInfo = Cell(cells, map, ColVendor),
                Status = Cell(cells, map, ColStatus),
                CurrentApprover = Cell(cells, map, ColApprover),
                Sa = Cell(cells, map, ColSa),
                Engineer1 = Cell(cells, map, ColEng1),
                Engineer2 = Cell(cells, map, ColEng2)
            });
        }
        return list;
    }

    public static string DecodeHeader(string raw) =>
        WebUtility.HtmlDecode(HtmlEntity.DeEntitize(raw ?? "")).Replace('\u00a0', ' ').Trim();

    public static string ParseVendorName(string? raw)
    {
        var text = (raw ?? "").Trim();
        var match = Regex.Match(text, @"^客戶名稱\s*[:：]\s*(.*)$", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : text;
    }

    private static string Cell(List<string> cells, Dictionary<string, int> map, string name) =>
        map.TryGetValue(name, out var i) && i < cells.Count ? cells[i] : "";

    private static List<HtmlNode> RowCells(HtmlNode row) =>
        row.SelectNodes("./th|./td")?.ToList() ?? [];

    private static string CellText(HtmlNode node)
    {
        var clone = node.Clone();
        foreach (var br in clone.SelectNodes(".//br") ?? Enumerable.Empty<HtmlNode>())
        {
            br.ParentNode?.ReplaceChild(HtmlNode.CreateNode("\n"), br);
        }
        var decoded = WebUtility.HtmlDecode(HtmlEntity.DeEntitize(clone.InnerText ?? "")).Replace('\u00a0', ' ');
        var lines = decoded.Split('\n').Select(line => Regex.Replace(line, "[ \\t]+", " ").Trim());
        return string.Join("\n", lines).Trim();
    }
}

public class UofIssueRow
{
    public int RowIndex { get; set; }
    public string IssueNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string DueDateText { get; set; } = "";
    public string VendorInfo { get; set; } = "";
    public string Status { get; set; } = "";
    public string CurrentApprover { get; set; } = "";
    public string Sa { get; set; } = "";
    public string Engineer1 { get; set; } = "";
    public string Engineer2 { get; set; } = "";
}
