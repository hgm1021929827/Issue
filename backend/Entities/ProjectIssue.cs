namespace Issue.Api.Entities;

public class ProjectIssue
{
    public long ProjectIssueId { get; set; }
    public long ProjectId { get; set; }
    public string SeqNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public long MajorCategoryId { get; set; }
    public long? SubCategoryId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string HandlerName { get; set; } = "";
    public bool IsCompleted { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public bool MissingKept { get; set; }
    public string Remark { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Project? Project { get; set; }
    public MajorCategory? MajorCategory { get; set; }
    public SubCategory? SubCategory { get; set; }
}
