namespace Issue.Api.Dtos;

public class MajorCategoryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public int SortOrder { get; set; }
}

public class MajorCategoryWriteDto
{
    public string Name { get; set; } = "";
}

public class SubCategoryDto
{
    public long Id { get; set; }
    public long MajorCategoryId { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public int SortOrder { get; set; }
}

public class SubCategoryWriteDto
{
    public long MajorCategoryId { get; set; }
    public string Name { get; set; } = "";
    public string? Color { get; set; }
}

public class SubCategoryUpdateDto
{
    public string? Name { get; set; }
    public string? Color { get; set; }
}


public class IssueWriteDto
{
    public string IssueNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Content { get; set; }
    public long MajorCategoryId { get; set; }
    public long? SubCategoryId { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Remark { get; set; }
    public long? ClientCompanyId { get; set; }
}

public class IssueDto
{
    public long Id { get; set; }
    public string IssueNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public long MajorCategoryId { get; set; }
    public string MajorCategoryName { get; set; } = "";
    public string MajorCategoryColor { get; set; } = "";
    public long? SubCategoryId { get; set; }
    public string? SubCategoryName { get; set; }
    public string SubCategoryColor { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public string? Remark { get; set; }
    public long? ClientCompanyId { get; set; }
    public string? ClientCompanyName { get; set; }
    public bool ImportedFromUof { get; set; }
    public bool MissingKept { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class IssueCalendarItemDto
{
    public long Id { get; set; }
    public long? PlanId { get; set; }
    public long? ProjectId { get; set; }
    public string Source { get; set; } = "issue";
    public string Kind { get; set; } = "due";
    public string? WorkType { get; set; }
    public long? WorkId { get; set; }
    public string Label { get; set; } = "";
    public string IssueNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateOnly Date { get; set; }
    public DateOnly DueDate { get; set; }
    public long MajorCategoryId { get; set; }
    public string MajorCategoryColor { get; set; } = "";
    public string SubCategoryColor { get; set; } = "";
}

public class IssuePlanWriteDto
{
    public DateOnly? Date { get; set; }
    public DateOnly? EndDate { get; set; }
    public List<DateOnly>? Dates { get; set; }
}

public class TodoWriteDto
{
    public long? ParentId { get; set; }
    public string Title { get; set; } = "";
    public string? Content { get; set; }
}

public class TodoUpdateDto
{
    public string Title { get; set; } = "";
    public string? Content { get; set; }
}

public class TodoCompleteDto
{
    public bool IsCompleted { get; set; }
}

public class TodoReorderDto
{
    public long? ParentId { get; set; }
    public List<long> OrderedIds { get; set; } = [];
}

public class TodoNodeDto
{
    public long Id { get; set; }
    public long IssueId { get; set; }
    public long? ParentId { get; set; }
    public bool IsCompleted { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public int SortOrder { get; set; }
    public List<TodoNodeDto> Children { get; set; } = [];
}

public class ProjectWriteDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public long MajorCategoryId { get; set; }
    public long? SubCategoryId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public long? ClientCompanyId { get; set; }
    public long? OwnerMemberId { get; set; }
}

public class ProjectIssueWriteDto
{
    public string SeqNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Content { get; set; }
    public long MajorCategoryId { get; set; }
    public long? SubCategoryId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? HandlerName { get; set; }
}

public class ProjectIssueDto
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string ProjectCode { get; set; } = "";
    public string SeqNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public long MajorCategoryId { get; set; }
    public string MajorCategoryName { get; set; } = "";
    public string MajorCategoryColor { get; set; } = "";
    public long? SubCategoryId { get; set; }
    public string? SubCategoryName { get; set; }
    public string SubCategoryColor { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public string HandlerName { get; set; } = "";
    public bool IsCompleted { get; set; }
    public bool MissingKept { get; set; }
    public string Remark { get; set; } = "";
    public decimal HoursTotal { get; set; }
    public long? ClientCompanyId { get; set; }
    public string? ClientCompanyName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ProjectListItemDto
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public long MajorCategoryId { get; set; }
    public string MajorCategoryName { get; set; } = "";
    public string MajorCategoryColor { get; set; } = "";
    public long? SubCategoryId { get; set; }
    public string? SubCategoryName { get; set; }
    public string SubCategoryColor { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public int ItemCount { get; set; }
    public int WorkItemCount { get; set; }
    public long? ClientCompanyId { get; set; }
    public string? ClientCompanyName { get; set; }
    public long? OwnerMemberId { get; set; }
    public string? OwnerMemberName { get; set; }
    public string? OwnerMemberEnglishName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CompanyMemberWriteDto
{
    public string Name { get; set; } = "";
    public string EnglishName { get; set; } = "";
    public string? Remark { get; set; }
}

public class CompanyMemberDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string EnglishName { get; set; } = "";
    public string Remark { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ClientCompanyWriteDto
{
    public string Name { get; set; } = "";
}

public class ClientCompanyListItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class ClientCompanyDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ClientContactWriteDto
{
    public string Name { get; set; } = "";
    public string? Title { get; set; }
}

public class ContactChannelWriteDto
{
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
}

public class ContactChannelDto
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
}

public class ClientContactDto
{
    public long Id { get; set; }
    public long ClientCompanyId { get; set; }
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public List<ContactChannelDto> Channels { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ClientCompanyTreeDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public List<ClientContactDto> Contacts { get; set; } = [];
}

public class ProjectDetailDto : ProjectListItemDto
{
    public List<ProjectIssueDto> Items { get; set; } = [];
}

public class TrackTodoWriteDto
{
    public string Title { get; set; } = "";
    public string? Content { get; set; }
    public string TargetType { get; set; } = "";
    public long? TargetId { get; set; }
    public DateOnly? ReminderDate { get; set; }
}

public class TrackTodoReorderDto
{
    public List<long> OrderedIds { get; set; } = [];
}

public class TrackTodoDto
{
    public long Id { get; set; }
    public string WorkType { get; set; } = "";
    public long WorkId { get; set; }
    public bool IsCompleted { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string TargetType { get; set; } = "";
    public long TargetId { get; set; }
    public string TargetName { get; set; } = "";
    public string TargetSecondaryName { get; set; } = "";
    public DateOnly? ReminderDate { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TrackTodoHomeItemDto
{
    public long Id { get; set; }
    public string WorkType { get; set; } = "";
    public long WorkId { get; set; }
    public long? ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string TargetType { get; set; } = "";
    public long TargetId { get; set; }
    public string TargetName { get; set; } = "";
    public string TargetSecondaryName { get; set; } = "";
    public string WorkLabel { get; set; } = "";
    public DateOnly? ReminderDate { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClientContactListItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class WorkItemDto
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string WorkItemCode { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal? PlannedDays { get; set; }
    public string OwnerName { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public bool MissingKept { get; set; }
    public string Remark { get; set; } = "";
    public decimal HoursTotal { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WorkItemRemarkWriteDto
{
    public string? Remark { get; set; }
}

public class WorkHourWriteDto
{
    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    public string? Remark { get; set; }
}

public class WorkHourDto
{
    public long Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    public string Remark { get; set; } = "";
}

public class ResolveProjectDto
{
    public string Status { get; set; } = "";
    public long? ProjectId { get; set; }
    public string ProjectCode { get; set; } = "";
    public string SuggestedName { get; set; } = "";
}

public class ImportSummaryDto
{
    public int WorkItemCreated { get; set; }
    public int WorkItemUpdated { get; set; }
    public int WorkItemSkipped { get; set; }
    public int IssueCreated { get; set; }
    public int IssueUpdated { get; set; }
    public int IssueSkipped { get; set; }
}

public class ImportRowErrorDto
{
    public string Sheet { get; set; } = "";
    public int RowIndex { get; set; }
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
}

public class ImportHourDiffDto
{
    public string OwnerKind { get; set; } = "";
    public long OwnerId { get; set; }
    public string OwnerLabel { get; set; } = "";
    public string Kind { get; set; } = "";
    public DateOnly? Date { get; set; }
    public decimal? ExcelHours { get; set; }
    public decimal? WebHours { get; set; }
    public string Detail { get; set; } = "";
    public string Remark { get; set; } = "";
}

public class ImportNotFoundDto
{
    public string OwnerKind { get; set; } = "";
    public long OwnerId { get; set; }
    public string OwnerLabel { get; set; } = "";
}

public class ImportRemarkLineDto
{
    public int Index { get; set; }
    public string Text { get; set; } = "";
    public string Kind { get; set; } = "";
    public DateOnly? Date { get; set; }
    public decimal? Hours { get; set; }
}

public class ImportHourReviewDto
{
    public string OwnerKind { get; set; } = "";
    public long OwnerId { get; set; }
    public string OwnerLabel { get; set; } = "";
    public string Remark { get; set; } = "";
    public List<ImportRemarkLineDto> Lines { get; set; } = [];
}

public class ImportResultDto
{
    public long ProjectId { get; set; }
    public ImportSummaryDto Summary { get; set; } = new();
    public List<ImportRowErrorDto> RowErrors { get; set; } = [];
    public List<ImportHourDiffDto> HourDiffs { get; set; } = [];
    public List<ImportHourReviewDto> HourReviews { get; set; } = [];
    public List<ImportNotFoundDto> NotFound { get; set; } = [];
}

public class ImportDecisionItemDto
{
    public string OwnerKind { get; set; } = "";
    public long OwnerId { get; set; }
    public string Action { get; set; } = "";
}

public class ImportDecisionRequestDto
{
    public List<ImportDecisionItemDto> Decisions { get; set; } = [];
}

public class IssueImportSummaryDto
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
}

public class IssueImportRowErrorDto
{
    public int RowIndex { get; set; }
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
}

public class IssueImportCreatedCompanyDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class IssueImportNotFoundDto
{
    public long Id { get; set; }
    public string IssueNo { get; set; } = "";
    public string Title { get; set; } = "";
}

public class IssueImportPendingCategoryDto
{
    public long Id { get; set; }
    public string IssueNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
}

public class IssueImportResultDto
{
    public IssueImportSummaryDto Summary { get; set; } = new();
    public List<IssueImportRowErrorDto> RowErrors { get; set; } = [];
    public List<IssueImportCreatedCompanyDto> CreatedCompanies { get; set; } = [];
    public List<IssueImportNotFoundDto> NotFound { get; set; } = [];
    public List<IssueImportPendingCategoryDto> PendingCategory { get; set; } = [];
    public long CountersignSubCategoryId { get; set; }
    public long DoneSubCategoryId { get; set; }
}

public class IssueImportDecisionItemDto
{
    public long Id { get; set; }
    public string Action { get; set; } = "";
}

public class IssueImportDecisionRequestDto
{
    public List<IssueImportDecisionItemDto> Decisions { get; set; } = [];
    public List<IssueImportDecisionItemDto> CategoryChoices { get; set; } = [];
    public long? CountersignSubCategoryId { get; set; }
    public long? DoneSubCategoryId { get; set; }
}
