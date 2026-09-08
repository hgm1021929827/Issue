namespace Issue.Api.Entities;

public class ContactChannel
{
    public long ContactChannelId { get; set; }
    public long ClientContactId { get; set; }
    public string ChannelType { get; set; } = "";
    public string ChannelValue { get; set; } = "";
    public ClientContact? ClientContact { get; set; }
}
