namespace Issue.Api.Entities;

public class ProjectWorkItem
{
    public long ProjectWorkItemId { get; set; }
    public long ProjectId { get; set; }
    public string WorkItemCode { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal? PlannedDays { get; set; }
    public string OwnerName { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public bool MissingKept { get; set; }
    public string Remark { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Project? Project { get; set; }
}
