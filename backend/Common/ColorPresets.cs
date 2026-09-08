namespace Issue.Api.Common;

public static class ColorPresets
{
    public static readonly string[] All =
    [
        "#7EB8D8",
        "#5BA3C9",
        "#8FCBE8",
        "#6BB3A8",
        "#8FA8C8",
        "#E0A86B",
        "#C48A9A",
        "#9B8FBF",
        "#7A9E6E",
        "#D4B06A"
    ];

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
}
