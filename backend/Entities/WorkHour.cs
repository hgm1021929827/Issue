namespace Issue.Api.Entities;

public class WorkHour
{
    public long WorkHourId { get; set; }
    public long? ProjectWorkItemId { get; set; }
    public long? ProjectIssueId { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal HourValue { get; set; }
    public string Remark { get; set; } = "";

    public ProjectWorkItem? ProjectWorkItem { get; set; }
    public ProjectIssue? ProjectIssue { get; set; }
}
