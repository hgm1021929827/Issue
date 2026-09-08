namespace Issue.Api.Entities;

public class ClientContact
{
    public long ClientContactId { get; set; }
    public long ClientCompanyId { get; set; }
    public string ContactName { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ClientCompany? ClientCompany { get; set; }
    public List<ContactChannel> Channels { get; set; } = [];
}
