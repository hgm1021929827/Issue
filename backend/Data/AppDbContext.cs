using Issue.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MajorCategory> MajorCategories => Set<MajorCategory>();
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<IssueItem> Issues => Set<IssueItem>();
    public DbSet<IssuePlan> IssuePlans => Set<IssuePlan>();
    public DbSet<IssueTodo> IssueTodos => Set<IssueTodo>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectIssue> ProjectIssues => Set<ProjectIssue>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();
    public DbSet<ClientCompany> ClientCompanies => Set<ClientCompany>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<ContactChannel> ContactChannels => Set<ContactChannel>();
    public DbSet<TrackTodo> TrackTodos => Set<TrackTodo>();
    public DbSet<ProjectWorkItem> ProjectWorkItems => Set<ProjectWorkItem>();
    public DbSet<WorkHour> WorkHours => Set<WorkHour>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MajorCategory>(e =>
        {
            e.ToTable("major_category");
            e.HasKey(x => x.MajorCategoryId);
            e.Property(x => x.MajorCategoryId).HasColumnName("major_category_id");
            e.Property(x => x.CategoryName).HasColumnName("category_name").HasMaxLength(50).IsRequired();
            e.Property(x => x.ColorHex).HasColumnName("color_hex").HasMaxLength(7).IsRequired();
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.HasIndex(x => x.CategoryName).IsUnique();
        });

        modelBuilder.Entity<SubCategory>(e =>
        {
            e.ToTable("sub_category");
            e.HasKey(x => x.SubCategoryId);
            e.Property(x => x.SubCategoryId).HasColumnName("sub_category_id");
            e.Property(x => x.MajorCategoryId).HasColumnName("major_category_id");
            e.Property(x => x.SubCategoryName).HasColumnName("sub_category_name").HasMaxLength(50).IsRequired();
            e.Property(x => x.ColorHex).HasColumnName("color_hex").HasMaxLength(7).IsRequired();
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.HasIndex(x => new { x.MajorCategoryId, x.SubCategoryName }).IsUnique();
            e.HasOne(x => x.MajorCategory).WithMany().HasForeignKey(x => x.MajorCategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IssueItem>(e =>
        {
            e.ToTable("issue");
            e.HasKey(x => x.IssueId);
            e.Property(x => x.IssueId).HasColumnName("issue_id");
            e.Property(x => x.IssueNo).HasColumnName("issue_no").HasMaxLength(50).IsRequired();
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(x => x.Content).HasColumnName("content").HasMaxLength(4000).IsRequired();
            e.Property(x => x.MajorCategoryId).HasColumnName("major_category_id");
            e.Property(x => x.SubCategoryId).HasColumnName("sub_category_id");
            e.Property(x => x.DueDate).HasColumnName("due_date");
            e.Property(x => x.Remark).HasColumnName("remark").HasMaxLength(2000);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.IssueNo).IsUnique();
            e.Property(x => x.ClientCompanyId).HasColumnName("client_company_id");
            e.Property(x => x.ImportedFromUof).HasColumnName("imported_from_uof");
            e.Property(x => x.MissingKept).HasColumnName("missing_kept");
            e.HasIndex(x => x.ClientCompanyId);
            e.HasOne(x => x.MajorCategory).WithMany().HasForeignKey(x => x.MajorCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SubCategory).WithMany().HasForeignKey(x => x.SubCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ClientCompany).WithMany().HasForeignKey(x => x.ClientCompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IssuePlan>(e =>
        {
            e.ToTable("issue_plan");
            e.HasKey(x => x.PlanId);
            e.Property(x => x.PlanId).HasColumnName("plan_id");
            e.Property(x => x.IssueId).HasColumnName("issue_id");
            e.Property(x => x.PlanDate).HasColumnName("plan_date");
            e.HasIndex(x => new { x.IssueId, x.PlanDate }).IsUnique();
            e.HasOne(x => x.Issue).WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IssueTodo>(e =>
        {
            e.ToTable("issue_todo");
            e.HasKey(x => x.TodoId);
            e.Property(x => x.TodoId).HasColumnName("todo_id");
            e.Property(x => x.IssueId).HasColumnName("issue_id");
            e.Property(x => x.ParentTodoId).HasColumnName("parent_todo_id");
            e.Property(x => x.IsCompleted).HasColumnName("is_completed");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(x => x.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.IssueId, x.ParentTodoId, x.SortOrder });
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("project");
            e.HasKey(x => x.ProjectId);
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.ProjectCode).HasColumnName("project_code").HasMaxLength(50).IsRequired();
            e.Property(x => x.ProjectName).HasColumnName("project_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
            e.Property(x => x.MajorCategoryId).HasColumnName("major_category_id");
            e.Property(x => x.SubCategoryId).HasColumnName("sub_category_id");
            e.Property(x => x.StartDate).HasColumnName("start_date");
            e.Property(x => x.DueDate).HasColumnName("due_date");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.ProjectCode).IsUnique();
            e.HasIndex(x => x.MajorCategoryId);
            e.HasIndex(x => x.DueDate);
            e.Property(x => x.ClientCompanyId).HasColumnName("client_company_id");
            e.Property(x => x.OwnerMemberId).HasColumnName("owner_member_id");
            e.HasIndex(x => x.ClientCompanyId);
            e.HasIndex(x => x.OwnerMemberId);
            e.HasOne(x => x.MajorCategory).WithMany().HasForeignKey(x => x.MajorCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SubCategory).WithMany().HasForeignKey(x => x.SubCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ClientCompany).WithMany().HasForeignKey(x => x.ClientCompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.OwnerMember).WithMany().HasForeignKey(x => x.OwnerMemberId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany<ProjectWorkItem>().WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectIssue>(e =>
        {
            e.ToTable("project_issue");
            e.HasKey(x => x.ProjectIssueId);
            e.Property(x => x.ProjectIssueId).HasColumnName("project_issue_id");
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.SeqNo).HasColumnName("seq_no").HasMaxLength(50).IsRequired();
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(x => x.Content).HasColumnName("content").HasMaxLength(4000).IsRequired();
            e.Property(x => x.MajorCategoryId).HasColumnName("major_category_id");
            e.Property(x => x.SubCategoryId).HasColumnName("sub_category_id");
            e.Property(x => x.StartDate).HasColumnName("start_date");
            e.Property(x => x.DueDate).HasColumnName("due_date");
            e.Property(x => x.HandlerName).HasColumnName("handler_name").HasMaxLength(50).IsRequired();
            e.Property(x => x.IsCompleted).HasColumnName("is_completed");
            e.Property(x => x.ActualStartDate).HasColumnName("actual_start_date");
            e.Property(x => x.ActualEndDate).HasColumnName("actual_end_date");
            e.Property(x => x.MissingKept).HasColumnName("missing_kept");
            e.Property(x => x.Remark).HasColumnName("remark").HasMaxLength(2000).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.ProjectId, x.SeqNo }).IsUnique();
            e.HasIndex(x => x.MajorCategoryId);
            e.HasIndex(x => x.DueDate);
            e.HasOne(x => x.MajorCategory).WithMany().HasForeignKey(x => x.MajorCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SubCategory).WithMany().HasForeignKey(x => x.SubCategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CompanyMember>(e =>
        {
            e.ToTable("company_member");
            e.HasKey(x => x.CompanyMemberId);
            e.Property(x => x.CompanyMemberId).HasColumnName("company_member_id");
            e.Property(x => x.MemberName).HasColumnName("member_name").HasMaxLength(50).IsRequired();
            e.Property(x => x.EnglishName).HasColumnName("english_name").HasMaxLength(50).IsRequired();
            e.Property(x => x.Remark).HasColumnName("remark").HasMaxLength(400).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.MemberName);
        });

        modelBuilder.Entity<ClientCompany>(e =>
        {
            e.ToTable("client_company");
            e.HasKey(x => x.ClientCompanyId);
            e.Property(x => x.ClientCompanyId).HasColumnName("client_company_id");
            e.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(100).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.CompanyName).IsUnique();
            e.HasMany(x => x.Contacts).WithOne(x => x.ClientCompany).HasForeignKey(x => x.ClientCompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClientContact>(e =>
        {
            e.ToTable("client_contact");
            e.HasKey(x => x.ClientContactId);
            e.Property(x => x.ClientContactId).HasColumnName("client_contact_id");
            e.Property(x => x.ClientCompanyId).HasColumnName("client_company_id");
            e.Property(x => x.ContactName).HasColumnName("contact_name").HasMaxLength(50).IsRequired();
            e.Property(x => x.JobTitle).HasColumnName("job_title").HasMaxLength(50).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.ClientCompanyId, x.ContactName }).IsUnique();
            e.HasMany(x => x.Channels).WithOne(x => x.ClientContact).HasForeignKey(x => x.ClientContactId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContactChannel>(e =>
        {
            e.ToTable("contact_channel");
            e.HasKey(x => x.ContactChannelId);
            e.Property(x => x.ContactChannelId).HasColumnName("contact_channel_id");
            e.Property(x => x.ClientContactId).HasColumnName("client_contact_id");
            e.Property(x => x.ChannelType).HasColumnName("channel_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.ChannelValue).HasColumnName("channel_value").HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<TrackTodo>(e =>
        {
            e.ToTable("track_todo");
            e.HasKey(x => x.TrackTodoId);
            e.Property(x => x.TrackTodoId).HasColumnName("track_todo_id");
            e.Property(x => x.IssueId).HasColumnName("issue_id");
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.ProjectIssueId).HasColumnName("project_issue_id");
            e.Property(x => x.ProjectWorkItemId).HasColumnName("project_work_item_id");
            e.Property(x => x.IsCompleted).HasColumnName("is_completed");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(x => x.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
            e.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.CompanyMemberId).HasColumnName("company_member_id");
            e.Property(x => x.ClientContactId).HasColumnName("client_contact_id");
            e.Property(x => x.ReminderDate).HasColumnName("reminder_date");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.HasIndex(x => new { x.IsCompleted, x.ReminderDate });
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.IssueId, x.SortOrder });
            e.HasIndex(x => new { x.ProjectId, x.SortOrder });
            e.HasIndex(x => new { x.ProjectIssueId, x.SortOrder });
            e.HasIndex(x => new { x.ProjectWorkItemId, x.SortOrder });
            e.HasIndex(x => new { x.IsCompleted, x.CreatedAt });
            e.HasIndex(x => x.CompanyMemberId);
            e.HasIndex(x => x.ClientContactId);
            // SQL Server 不允許多條 CASCADE 路徑；刪除由服務層先清 TrackTodo
            e.HasOne(x => x.Issue).WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProjectIssue).WithMany().HasForeignKey(x => x.ProjectIssueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProjectWorkItem).WithMany().HasForeignKey(x => x.ProjectWorkItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CompanyMember).WithMany().HasForeignKey(x => x.CompanyMemberId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ClientContact).WithMany().HasForeignKey(x => x.ClientContactId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectWorkItem>(e =>
        {
            e.ToTable("project_work_item");
            e.HasKey(x => x.ProjectWorkItemId);
            e.Property(x => x.ProjectWorkItemId).HasColumnName("project_work_item_id");
            e.Property(x => x.ProjectId).HasColumnName("project_id");
            e.Property(x => x.WorkItemCode).HasColumnName("work_item_code").HasMaxLength(50).IsRequired();
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(4000).IsRequired();
            e.Property(x => x.PlannedDays).HasColumnName("planned_days").HasPrecision(8, 2);
            e.Property(x => x.OwnerName).HasColumnName("owner_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.StartDate).HasColumnName("start_date");
            e.Property(x => x.DueDate).HasColumnName("due_date");
            e.Property(x => x.IsCompleted).HasColumnName("is_completed");
            e.Property(x => x.ActualStartDate).HasColumnName("actual_start_date");
            e.Property(x => x.ActualEndDate).HasColumnName("actual_end_date");
            e.Property(x => x.MissingKept).HasColumnName("missing_kept");
            e.Property(x => x.Remark).HasColumnName("remark").HasMaxLength(2000).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.ProjectId, x.WorkItemCode }).IsUnique();
            e.HasIndex(x => new { x.ProjectId, x.DueDate });
        });

        modelBuilder.Entity<WorkHour>(e =>
        {
            e.ToTable("work_hour");
            e.HasKey(x => x.WorkHourId);
            e.Property(x => x.WorkHourId).HasColumnName("work_hour_id");
            e.Property(x => x.ProjectWorkItemId).HasColumnName("project_work_item_id");
            e.Property(x => x.ProjectIssueId).HasColumnName("project_issue_id");
            e.Property(x => x.WorkDate).HasColumnName("work_date");
            e.Property(x => x.HourValue).HasColumnName("hour_value").HasPrecision(6, 2);
            e.Property(x => x.Remark).HasColumnName("remark").HasMaxLength(2000).IsRequired();
            e.HasIndex(x => new { x.ProjectWorkItemId, x.WorkDate }).IsUnique();
            e.HasIndex(x => new { x.ProjectIssueId, x.WorkDate }).IsUnique();
            // SQL Server：避免經 project → work_item／issue 再進 work_hour 的多重 CASCADE
            e.HasOne(x => x.ProjectWorkItem).WithMany().HasForeignKey(x => x.ProjectWorkItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProjectIssue).WithMany().HasForeignKey(x => x.ProjectIssueId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
