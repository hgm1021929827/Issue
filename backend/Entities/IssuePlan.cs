namespace Issue.Api.Entities;

public class IssuePlan
{
    public long PlanId { get; set; }
    public long IssueId { get; set; }
    public DateOnly PlanDate { get; set; }
    public IssueItem? Issue { get; set; }
}
