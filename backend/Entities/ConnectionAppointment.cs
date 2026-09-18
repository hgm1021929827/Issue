namespace Issue.Api.Entities;

public class ConnectionAppointment
{
    public long ConnectionAppointmentId { get; set; }
    public long ClientCompanyId { get; set; }
    public long ClientContactId { get; set; }
    public long? ContactChannelId { get; set; }
    public long? SubCategoryId { get; set; }
    public DateOnly? AppointmentDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ClientCompany? ClientCompany { get; set; }
    public ClientContact? ClientContact { get; set; }
    public ContactChannel? ContactChannel { get; set; }
    public SubCategory? SubCategory { get; set; }
    public List<ConnectionAppointmentItem> Items { get; set; } = [];
}
