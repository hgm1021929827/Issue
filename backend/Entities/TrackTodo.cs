namespace Issue.Api.Entities;

public class TrackTodo
{
    public long TrackTodoId { get; set; }
    public long? IssueId { get; set; }
    public long? ProjectId { get; set; }
    public long? ProjectIssueId { get; set; }
    public long? ProjectWorkItemId { get; set; }
    public bool IsCompleted { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string TargetType { get; set; } = "";
    public long? CompanyMemberId { get; set; }
    public long? ClientContactId { get; set; }
    public DateOnly? ReminderDate { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public IssueItem? Issue { get; set; }
    public Project? Project { get; set; }
    public ProjectIssue? ProjectIssue { get; set; }
    public ProjectWorkItem? ProjectWorkItem { get; set; }
    public CompanyMember? CompanyMember { get; set; }
    public ClientContact? ClientContact { get; set; }
}
