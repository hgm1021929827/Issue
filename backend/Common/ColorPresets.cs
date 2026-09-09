namespace Issue.Api.Common;

public static class ColorPresets
{
    /// <summary>空白小分類／無法辨識時的預設色（霧藍）。</summary>
    public const string Default = "#91BFD5";

    /// <summary>
    /// 低飽和粉彩備選色，由冷至暖、相近色系排列。
    /// </summary>
    public static readonly string[] All =
    [
        "#A9D6E8", // 粉藍
        "#91BFD5", // 霧藍
        "#B8B4CF", // 灰紫
        "#C7BAD9", // 薰衣草紫
        "#A8D8CF", // 薄荷綠
        "#B7CDB1", // 鼠尾草綠
        "#E8D5A9", // 奶油黃
        "#EBC5A5", // 杏桃橘
        "#DFB6BE", // 霧粉紅
        "#D6C1A5"  // 奶茶色
    ];

    private static readonly Dictionary<string, string> LegacyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["#7EB8D8"] = "#A9D6E8",
        ["#5BA3C9"] = "#91BFD5",
        ["#8FCBE8"] = "#A9D6E8",
        ["#6BB3A8"] = "#A8D8CF",
        ["#8FA8C8"] = "#91BFD5",
        ["#E0A86B"] = "#EBC5A5",
        ["#C48A9A"] = "#DFB6BE",
        ["#9B8FBF"] = "#C7BAD9",
        ["#7A9E6E"] = "#B7CDB1",
        ["#D4B06A"] = "#E8D5A9"
    };

    public static bool IsAllowed(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return false;
        }
        return All.Any(x => x.Equals(color.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string color) =>
        All.First(x => x.Equals(color.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>將第一階段舊備選色對應到新粉彩色盤；已是新色則原樣回傳；無法對應則用預設。</summary>
    public static string MapOrDefault(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return Default;
        }
        var trimmed = color.Trim();
        if (IsAllowed(trimmed))
        {
            return Normalize(trimmed);
        }
        if (LegacyMap.TryGetValue(trimmed, out var mapped))
        {
            return mapped;
        }
        return Default;
    }
}
