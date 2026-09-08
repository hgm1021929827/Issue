namespace Issue.Api.Entities;

public class MajorCategory
{
    public long MajorCategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string ColorHex { get; set; } = "";
    public int SortOrder { get; set; }
}
