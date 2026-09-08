namespace Issue.Api.Entities;

public class IssueTodo
{
    public long TodoId { get; set; }
    public long IssueId { get; set; }
    public long? ParentTodoId { get; set; }
    public bool IsCompleted { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
