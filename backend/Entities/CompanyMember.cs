namespace Issue.Api.Entities;

public class CompanyMember
{
    public long CompanyMemberId { get; set; }
    public string MemberName { get; set; } = "";
    public string EnglishName { get; set; } = "";
    public string Remark { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
