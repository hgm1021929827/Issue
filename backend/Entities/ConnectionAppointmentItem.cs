namespace Issue.Api.Entities;

public class ConnectionAppointmentItem
{
    public long ItemId { get; set; }
    public long ConnectionAppointmentId { get; set; }
    public long? IssueId { get; set; }
    public long? ProjectId { get; set; }
    public long? TodoId { get; set; }
    public int SortOrder { get; set; }

    public ConnectionAppointment? Appointment { get; set; }
    public IssueItem? Issue { get; set; }
    public Project? Project { get; set; }
    public IssueTodo? Todo { get; set; }
}
