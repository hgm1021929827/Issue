namespace Issue.Api.Entities;

public class SubCategory
{
    public long SubCategoryId { get; set; }
    public long MajorCategoryId { get; set; }
    public string SubCategoryName { get; set; } = "";
    public string ColorHex { get; set; } = "#91BFD5";
    public int SortOrder { get; set; }
    public MajorCategory? MajorCategory { get; set; }
}
