namespace Issue.Api.Entities;

public class IssueItem
{
    public long IssueId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string IssueNo { get; set; } = "";
    public long MajorCategoryId { get; set; }
    public long? SubCategoryId { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Remark { get; set; }
    public long? ClientCompanyId { get; set; }
    public bool ImportedFromUof { get; set; }
    public bool MissingKept { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public MajorCategory? MajorCategory { get; set; }
    public SubCategory? SubCategory { get; set; }
    public ClientCompany? ClientCompany { get; set; }
}
