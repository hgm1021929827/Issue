using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Data;

public static partial class DbSeeder
{
    static void EnsureSchemaSqlServer(AppDbContext db)
    {
        SqlAddColumnIfMissing(db, "sub_category", "color_hex",
            "ALTER TABLE [sub_category] ADD [color_hex] NVARCHAR(7) NOT NULL CONSTRAINT DF_sub_category_color_hex DEFAULT N'#91BFD5'");
        SqlAddColumnIfMissing(db, "issue", "issue_no",
            "ALTER TABLE [issue] ADD [issue_no] NVARCHAR(50) NULL");
        db.Database.ExecuteSqlRaw("""
            UPDATE [issue]
            SET [issue_no] = CAST([issue_id] AS NVARCHAR(50))
            WHERE [issue_no] IS NULL OR [issue_no] = N'';
            """);
        db.Database.ExecuteSqlRaw("""
            IF EXISTS (
              SELECT 1 FROM sys.columns
              WHERE object_id = OBJECT_ID(N'dbo.issue') AND name = N'issue_no' AND is_nullable = 1
            )
            BEGIN
              ALTER TABLE [issue] ALTER COLUMN [issue_no] NVARCHAR(50) NOT NULL;
            END
            """);
        SqlAddIndexIfMissing(db, "issue", "uk_issue_no",
            "CREATE UNIQUE INDEX [uk_issue_no] ON [issue] ([issue_no])");
        db.Database.ExecuteSqlRaw("""
            IF COL_LENGTH(N'dbo.major_category', N'category_name') IS NOT NULL
            BEGIN
              ALTER TABLE [major_category] ALTER COLUMN [category_name] NVARCHAR(50) NOT NULL;
            END
            """);
        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'dbo.issue_plan', N'U') IS NULL
            BEGIN
              CREATE TABLE [issue_plan] (
                [plan_id] BIGINT NOT NULL IDENTITY(1,1),
                [issue_id] BIGINT NOT NULL,
                [plan_date] DATE NOT NULL,
                CONSTRAINT [PK_issue_plan] PRIMARY KEY ([plan_id]),
                CONSTRAINT [uk_issue_plan_issue_date] UNIQUE ([issue_id], [plan_date]),
                CONSTRAINT [fk_issue_plan_issue] FOREIGN KEY ([issue_id]) REFERENCES [issue] ([issue_id]) ON DELETE CASCADE
              );
            END
            """);
        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'dbo.project', N'U') IS NULL
            BEGIN
              CREATE TABLE [project] (
                [project_id] BIGINT NOT NULL IDENTITY(1,1),
                [project_code] NVARCHAR(50) NOT NULL,
                [project_name] NVARCHAR(200) NOT NULL,
                [description] NVARCHAR(4000) NOT NULL CONSTRAINT DF_project_description DEFAULT N'',
                [major_category_id] BIGINT NOT NULL,
                [sub_category_id] BIGINT NULL,
                [start_date] DATE NULL,
                [due_date] DATE NULL,
                [created_at] DATETIME2 NOT NULL,
                [updated_at] DATETIME2 NOT NULL,
                CONSTRAINT [PK_project] PRIMARY KEY ([project_id]),
                CONSTRAINT [uk_project_code] UNIQUE ([project_code]),
                CONSTRAINT [fk_project_major] FOREIGN KEY ([major_category_id]) REFERENCES [major_category] ([major_category_id]),
                CONSTRAINT [fk_project_sub] FOREIGN KEY ([sub_category_id]) REFERENCES [sub_category] ([sub_category_id])
              );
              CREATE INDEX [ix_project_major] ON [project] ([major_category_id]);
              CREATE INDEX [ix_project_due] ON [project] ([due_date]);
            END
            """);
        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'dbo.project_issue', N'U') IS NULL
            BEGIN
              CREATE TABLE [project_issue] (
                [project_issue_id] BIGINT NOT NULL IDENTITY(1,1),
                [project_id] BIGINT NOT NULL,
                [seq_no] NVARCHAR(50) NOT NULL,
                [title] NVARCHAR(200) NOT NULL,
                [content] NVARCHAR(4000) NOT NULL CONSTRAINT DF_project_issue_content DEFAULT N'',
                [major_category_id] BIGINT NOT NULL,
                [sub_category_id] BIGINT NULL,
                [start_date] DATE NULL,
                [due_date] DATE NULL,
                [created_at] DATETIME2 NOT NULL,
                [updated_at] DATETIME2 NOT NULL,
                CONSTRAINT [PK_project_issue] PRIMARY KEY ([project_issue_id]),
                CONSTRAINT [uk_project_issue_seq] UNIQUE ([project_id], [seq_no]),
                CONSTRAINT [fk_project_issue_project] FOREIGN KEY ([project_id]) REFERENCES [project] ([project_id]) ON DELETE CASCADE,
                CONSTRAINT [fk_project_issue_major] FOREIGN KEY ([major_category_id]) REFERENCES [major_category] ([major_category_id]),
                CONSTRAINT [fk_project_issue_sub] FOREIGN KEY ([sub_category_id]) REFERENCES [sub_category] ([sub_category_id])
              );
              CREATE INDEX [ix_project_issue_major] ON [project_issue] ([major_category_id]);
              CREATE INDEX [ix_project_issue_due] ON [project_issue] ([due_date]);
            END
            """);
        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'dbo.company_member', N'U') IS NULL
            BEGIN
              CREATE TABLE [company_member] (
                [company_member_id] BIGINT NOT NULL IDENTITY(1,1),
                [member_name] NVARCHAR(50) NOT NULL,
                [english_name] NVARCHAR(50) NOT NULL,
                [remark] NVARCHAR(400) NOT NULL CONSTRAINT DF_company_member_remark DEFAULT N'',
                [created_at] DATETIME2 NOT NULL,
                [updated_at] DATETIME2 NOT NULL,
                CONSTRAINT [PK_company_member] PRIMARY KEY ([company_member_id])
              );
              CREATE INDEX [ix_company_member_name] ON [company_member] ([member_name]);
            END
            """);
        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'dbo.client_company', N'U') IS NULL
            BEGIN
              CREATE TABLE [client_company] (
                [client_company_id] BIGINT NOT NULL IDENTITY(1,1),
                [company_name] NVARCHAR(100) NOT NULL,
                [created_at] DATETIME2 NOT NULL,
                [updated_at] DATETIME2 NOT NULL,
                CONSTRAINT [PK_client_company] PRIMARY KEY ([client_company_id]),
                CONSTRAINT [uk_client_company_name] UNIQUE ([company_name])
              );
            END
            """);
        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'dbo.client_contact', N'U') IS NULL
            BEGIN
              CREATE TABLE [client_contact] (
                [client_contact_id] BIGINT NOT NULL IDENTITY(1,1),
                [client_company_id] BIGINT NOT NULL,
                [contact_name] NVARCHAR(50) NOT NULL,
                [job_title] NVARCHAR(50) NOT NULL CONSTRAINT DF_client_contact_job_title DEFAULT N'',
                [created_at] DATETIME2 NOT NULL,
                [updated_at] DATETIME2 NOT NULL,
                CONSTRAINT [PK_client_contact] PRIMARY KEY ([client_contact_id]),
                CONSTRAINT [uk_client_contact_name] UNIQUE ([client_company_id], [contact_name]),
                CONSTRAINT [fk_client_contact_company] FOREIGN KEY ([client_company_id]) REFERENCES [client_company] ([client_company_id]) ON DELETE CASCADE
              );
            END
            """);
        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'dbo.contact_channel', N'U') IS NULL
            BEGIN
              CREATE TABLE [contact_channel] (
                [contact_channel_id] BIGINT NOT NULL IDENTITY(1,1),
                [client_contact_id] BIGINT NOT NULL,
                [channel_type] NVARCHAR(20) NOT NULL,
                [channel_value] NVARCHAR(200) NOT NULL,
                CONSTRAINT [PK_contact_channel] PRIMARY KEY ([contact_channel_id]),
                CONSTRAINT [fk_contact_channel_contact] FOREIGN KEY ([client_contact_id]) REFERENCES [client_contact] ([client_contact_id]) ON DELETE CASCADE
              );
            END
            """);
        SqlAddColumnIfMissing(db, "project", "client_company_id",
            "ALTER TABLE [project] ADD [client_company_id] BIGINT NULL");
        SqlAddIndexIfMissing(db, "project", "ix_project_company",
            "CREATE INDEX [ix_project_company] ON [project] ([client_company_id])");
        SqlAddColumnIfMissing(db, "project", "owner_member_id",
            "ALTER TABLE [project] ADD [owner_member_id] BIGINT NULL");
        SqlAddIndexIfMissing(db, "project", "ix_project_owner",
            "CREATE INDEX [ix_project_owner] ON [project] ([owner_member_id])");
        SqlAddColumnIfMissing(db, "issue", "imported_from_uof",
            "ALTER TABLE [issue] ADD [imported_from_uof] BIT NOT NULL CONSTRAINT DF_issue_imported_from_uof DEFAULT 0");
        SqlAddColumnIfMissing(db, "issue", "missing_kept",
            "ALTER TABLE [issue] ADD [missing_kept] BIT NOT NULL CONSTRAINT DF_issue_missing_kept DEFAULT 0");
        SqlAddColumnIfMissing(db, "issue", "client_company_id",
            "ALTER TABLE [issue] ADD [client_company_id] BIGINT NULL");
        SqlAddIndexIfMissing(db, "issue", "ix_issue_company",
            "CREATE INDEX [ix_issue_company] ON [issue] ([client_company_id])");
        SqlAddFkIfMissing(db, "fk_project_company",
            "ALTER TABLE [project] ADD CONSTRAINT [fk_project_company] FOREIGN KEY ([client_company_id]) REFERENCES [client_company] ([client_company_id])");
        SqlAddFkIfMissing(db, "fk_project_owner",
            "ALTER TABLE [project] ADD CONSTRAINT [fk_project_owner] FOREIGN KEY ([owner_member_id]) REFERENCES [company_member] ([company_member_id])");
        SqlAddFkIfMissing(db, "fk_issue_company",
            "ALTER TABLE [issue] ADD CONSTRAINT [fk_issue_company] FOREIGN KEY ([client_company_id]) REFERENCES [client_company] ([client_company_id])");
        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'dbo.track_todo', N'U') IS NULL
            BEGIN
              CREATE TABLE [track_todo] (
                [track_todo_id] BIGINT NOT NULL IDENTITY(1,1),
                [issue_id] BIGINT NULL,
                [project_id] BIGINT NULL,
                [project_issue_id] BIGINT NULL,
                [is_completed] BIT NOT NULL CONSTRAINT DF_track_todo_is_completed DEFAULT 0,
                [title] NVARCHAR(200) NOT NULL,
                [content] NVARCHAR(2000) NOT NULL CONSTRAINT DF_track_todo_content DEFAULT N'',
                [target_type] NVARCHAR(20) NOT NULL,
                [company_member_id] BIGINT NULL,
                [client_contact_id] BIGINT NULL,
                [reminder_date] DATE NULL,
                [sort_order] INT NOT NULL,
                [created_at] DATETIME2 NOT NULL,
                [updated_at] DATETIME2 NOT NULL,
                CONSTRAINT [PK_track_todo] PRIMARY KEY ([track_todo_id]),
                CONSTRAINT [fk_track_todo_issue] FOREIGN KEY ([issue_id]) REFERENCES [issue] ([issue_id]),
                CONSTRAINT [fk_track_todo_project] FOREIGN KEY ([project_id]) REFERENCES [project] ([project_id]),
                CONSTRAINT [fk_track_todo_item] FOREIGN KEY ([project_issue_id]) REFERENCES [project_issue] ([project_issue_id]),
                CONSTRAINT [fk_track_todo_member] FOREIGN KEY ([company_member_id]) REFERENCES [company_member] ([company_member_id]),
                CONSTRAINT [fk_track_todo_contact] FOREIGN KEY ([client_contact_id]) REFERENCES [client_contact] ([client_contact_id]),
                CONSTRAINT [ck_track_todo_work] CHECK (
                  (issue_id IS NOT NULL AND project_id IS NULL AND project_issue_id IS NULL)
                  OR (issue_id IS NULL AND project_id IS NOT NULL AND project_issue_id IS NULL)
                  OR (issue_id IS NULL AND project_id IS NULL AND project_issue_id IS NOT NULL)
                ),
                CONSTRAINT [ck_track_todo_target] CHECK (
                  (target_type = N'member' AND company_member_id IS NOT NULL AND client_contact_id IS NULL)
                  OR (target_type = N'clientContact' AND client_contact_id IS NOT NULL AND company_member_id IS NULL)
                )
              );
              CREATE INDEX [ix_track_todo_issue] ON [track_todo] ([issue_id], [sort_order]);
              CREATE INDEX [ix_track_todo_project] ON [track_todo] ([project_id], [sort_order]);
              CREATE INDEX [ix_track_todo_item] ON [track_todo] ([project_issue_id], [sort_order]);
              CREATE INDEX [ix_track_todo_home] ON [track_todo] ([is_completed], [created_at]);
              CREATE INDEX [ix_track_todo_member] ON [track_todo] ([company_member_id]);
              CREATE INDEX [ix_track_todo_contact] ON [track_todo] ([client_contact_id]);
              CREATE INDEX [ix_track_todo_reminder] ON [track_todo] ([is_completed], [reminder_date]);
            END
            """);
        SqlAddColumnIfMissing(db, "track_todo", "reminder_date",
            "ALTER TABLE [track_todo] ADD [reminder_date] DATE NULL");
        SqlAddIndexIfMissing(db, "track_todo", "ix_track_todo_reminder",
            "CREATE INDEX [ix_track_todo_reminder] ON [track_todo] ([is_completed], [reminder_date])");
        SqlAddColumnIfMissing(db, "project_issue", "handler_name",
            "ALTER TABLE [project_issue] ADD [handler_name] NVARCHAR(50) NOT NULL CONSTRAINT DF_project_issue_handler_name DEFAULT N''");
        SqlAddColumnIfMissing(db, "project_issue", "is_completed",
            "ALTER TABLE [project_issue] ADD [is_completed] BIT NOT NULL CONSTRAINT DF_project_issue_is_completed DEFAULT 0");
        SqlAddColumnIfMissing(db, "project_issue", "actual_start_date",
            "ALTER TABLE [project_issue] ADD [actual_start_date] DATE NULL");
        SqlAddColumnIfMissing(db, "project_issue", "actual_end_date",
            "ALTER TABLE [project_issue] ADD [actual_end_date] DATE NULL");
        SqlAddColumnIfMissing(db, "project_issue", "missing_kept",
            "ALTER TABLE [project_issue] ADD [missing_kept] BIT NOT NULL CONSTRAINT DF_project_issue_missing_kept DEFAULT 0");
        SqlAddColumnIfMissing(db, "project_issue", "remark",
            "ALTER TABLE [project_issue] ADD [remark] NVARCHAR(2000) NOT NULL CONSTRAINT DF_project_issue_remark DEFAULT N''");
        SqlAddColumnIfMissing(db, "project_work_item", "remark",
            "ALTER TABLE [project_work_item] ADD [remark] NVARCHAR(2000) NOT NULL CONSTRAINT DF_project_work_item_remark DEFAULT N''");
        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'dbo.project_work_item', N'U') IS NULL
            BEGIN
              CREATE TABLE [project_work_item] (
                [project_work_item_id] BIGINT NOT NULL IDENTITY(1,1),
                [project_id] BIGINT NOT NULL,
                [work_item_code] NVARCHAR(50) NOT NULL,
                [title] NVARCHAR(4000) NOT NULL CONSTRAINT DF_project_work_item_title DEFAULT N'',
                [planned_days] DECIMAL(8,2) NULL,
                [owner_name] NVARCHAR(200) NOT NULL CONSTRAINT DF_project_work_item_owner_name DEFAULT N'',
                [start_date] DATE NULL,
                [due_date] DATE NULL,
                [is_completed] BIT NOT NULL CONSTRAINT DF_project_work_item_is_completed DEFAULT 0,
                [actual_start_date] DATE NULL,
                [actual_end_date] DATE NULL,
                [missing_kept] BIT NOT NULL CONSTRAINT DF_project_work_item_missing_kept DEFAULT 0,
                [remark] NVARCHAR(2000) NOT NULL CONSTRAINT DF_project_work_item_remark2 DEFAULT N'',
                [created_at] DATETIME2 NOT NULL,
                [updated_at] DATETIME2 NOT NULL,
                CONSTRAINT [PK_project_work_item] PRIMARY KEY ([project_work_item_id]),
                CONSTRAINT [uk_project_work_item_code] UNIQUE ([project_id], [work_item_code]),
                CONSTRAINT [fk_project_work_item_project] FOREIGN KEY ([project_id]) REFERENCES [project] ([project_id]) ON DELETE CASCADE
              );
              CREATE INDEX [ix_project_work_item_due] ON [project_work_item] ([project_id], [due_date]);
            END
            """);
        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'dbo.work_hour', N'U') IS NULL
            BEGIN
              CREATE TABLE [work_hour] (
                [work_hour_id] BIGINT NOT NULL IDENTITY(1,1),
                [project_work_item_id] BIGINT NULL,
                [project_issue_id] BIGINT NULL,
                [work_date] DATE NOT NULL,
                [hour_value] DECIMAL(6,2) NOT NULL,
                [remark] NVARCHAR(2000) NOT NULL CONSTRAINT DF_work_hour_remark DEFAULT N'',
                CONSTRAINT [PK_work_hour] PRIMARY KEY ([work_hour_id]),
                CONSTRAINT [uk_work_hour_item] UNIQUE ([project_work_item_id], [work_date]),
                CONSTRAINT [uk_work_hour_issue] UNIQUE ([project_issue_id], [work_date]),
                CONSTRAINT [fk_work_hour_item] FOREIGN KEY ([project_work_item_id]) REFERENCES [project_work_item] ([project_work_item_id]),
                CONSTRAINT [fk_work_hour_issue] FOREIGN KEY ([project_issue_id]) REFERENCES [project_issue] ([project_issue_id]),
                CONSTRAINT [ck_work_hour_target] CHECK (
                  (project_work_item_id IS NOT NULL AND project_issue_id IS NULL)
                  OR (project_work_item_id IS NULL AND project_issue_id IS NOT NULL)
                ),
                CONSTRAINT [ck_work_hour_value] CHECK ([hour_value] > 0 AND [hour_value] <= 999.99)
              );
            END
            """);
        SqlAddColumnIfMissing(db, "track_todo", "project_work_item_id",
            "ALTER TABLE [track_todo] ADD [project_work_item_id] BIGINT NULL");
        SqlAddIndexIfMissing(db, "track_todo", "ix_track_todo_work_item",
            "CREATE INDEX [ix_track_todo_work_item] ON [track_todo] ([project_work_item_id], [sort_order])");
        SqlAddFkIfMissing(db, "fk_track_todo_work_item",
            "ALTER TABLE [track_todo] ADD CONSTRAINT [fk_track_todo_work_item] FOREIGN KEY ([project_work_item_id]) REFERENCES [project_work_item] ([project_work_item_id])");
        SqlDropCheckIfExists(db, "track_todo", "ck_track_todo_work");
        SqlAddCheckIfMissing(db, "track_todo", "ck_track_todo_work", """
            ALTER TABLE [track_todo] ADD CONSTRAINT [ck_track_todo_work] CHECK (
              (issue_id IS NOT NULL AND project_id IS NULL AND project_issue_id IS NULL AND project_work_item_id IS NULL)
              OR (issue_id IS NULL AND project_id IS NOT NULL AND project_issue_id IS NULL AND project_work_item_id IS NULL)
              OR (issue_id IS NULL AND project_id IS NULL AND project_issue_id IS NOT NULL AND project_work_item_id IS NULL)
              OR (issue_id IS NULL AND project_id IS NULL AND project_issue_id IS NULL AND project_work_item_id IS NOT NULL)
            )
            """);
    }

    private static void SqlAddColumnIfMissing(AppDbContext db, string table, string column, string alterSql)
    {
        db.Database.ExecuteSqlRaw($"""
            IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL
            BEGIN
              {alterSql};
            END
            """);
    }

    private static void SqlAddFkIfMissing(AppDbContext db, string constraint, string alterSql)
    {
        db.Database.ExecuteSqlRaw($"""
            IF NOT EXISTS (
              SELECT 1 FROM sys.foreign_keys WHERE name = N'{constraint}'
            )
            BEGIN
              {alterSql};
            END
            """);
    }

    private static void SqlAddIndexIfMissing(AppDbContext db, string table, string index, string createSql)
    {
        db.Database.ExecuteSqlRaw($"""
            IF NOT EXISTS (
              SELECT 1 FROM sys.indexes
              WHERE name = N'{index}' AND object_id = OBJECT_ID(N'dbo.{table}')
            )
            BEGIN
              {createSql};
            END
            """);
    }

    private static void SqlDropCheckIfExists(AppDbContext db, string table, string constraint)
    {
        db.Database.ExecuteSqlRaw($"""
            IF EXISTS (
              SELECT 1 FROM sys.check_constraints
              WHERE name = N'{constraint}' AND parent_object_id = OBJECT_ID(N'dbo.{table}')
            )
            BEGIN
              ALTER TABLE [{table}] DROP CONSTRAINT [{constraint}];
            END
            """);
    }

    private static void SqlAddCheckIfMissing(AppDbContext db, string table, string constraint, string alterSql)
    {
        db.Database.ExecuteSqlRaw($"""
            IF NOT EXISTS (
              SELECT 1 FROM sys.check_constraints
              WHERE name = N'{constraint}' AND parent_object_id = OBJECT_ID(N'dbo.{table}')
            )
            BEGIN
              {alterSql};
            END
            """);
    }
}
