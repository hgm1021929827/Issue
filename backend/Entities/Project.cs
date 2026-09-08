namespace Issue.Api.Entities;

public class Project
{
    public long ProjectId { get; set; }
    public string ProjectCode { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string Description { get; set; } = "";
    public long MajorCategoryId { get; set; }
    public long? SubCategoryId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public long? ClientCompanyId { get; set; }
    public long? OwnerMemberId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public MajorCategory? MajorCategory { get; set; }
    public SubCategory? SubCategory { get; set; }
    public ClientCompany? ClientCompany { get; set; }
    public CompanyMember? OwnerMember { get; set; }
    public List<ProjectIssue> Items { get; set; } = [];
}
