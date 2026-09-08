namespace Issue.Api.Entities;

public class ClientCompany
{
    public long ClientCompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ClientContact> Contacts { get; set; } = [];
}
